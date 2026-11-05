"""Offline music contract, authentication and no-double-submit tests. No cloud or provider calls."""
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import threading
import unittest
import urllib.error
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("workshop_music", Path(__file__).with_name("music.py"))
music = importlib.util.module_from_spec(spec)
spec.loader.exec_module(music)


class MusicTests(unittest.TestCase):
    def setUp(self):
        self.env = patch.dict(os.environ, {"MUSIC_TEST_CODE": "offline-only", "RUNNINGHUB_AI_KEY": "not-a-live-key",
            "MUSIC_MAX_USD": ".10", "MUSIC_DAILY_LIMIT": "2", "MUSIC_GLOBAL_LIMIT": "10"})
        self.env.start(); self.addCleanup(self.env.stop)
        self.objects = {}; self.revisions = {}; self.submits = 0; self.quote = .06; self.fail_submit = False
        self.storage_lock = threading.Lock()
        self.real_cos = music._cos
        self.cos = patch.object(music, "_cos", self.fake_cos); self.cos.start(); self.addCleanup(self.cos.stop)
        self.rh = patch.object(music, "_rh", self.fake_rh); self.rh.start(); self.addCleanup(self.rh.stop)

    def fake_cos(self, method, key, data=None, create=False, etag=None):
        if etag is not None: raise AssertionError("Do not rely on COS PUT ETag checks")
        with self.storage_lock:
            if method == "GET":
                if key not in self.objects: raise urllib.error.HTTPError(key, 404, "not found", {}, io.BytesIO())
                return copy.deepcopy(self.objects[key]), str(self.revisions[key])
            if create and key in self.objects: raise urllib.error.HTTPError(key, 409, "exists", {}, io.BytesIO())
            self.objects[key] = copy.deepcopy(data); self.revisions[key] = self.revisions.get(key, 0) + 1
            return None, str(self.revisions[key])

    def fake_rh(self, path, body):
        if path.startswith("price-preview/"): return {"estimatedPrice": self.quote, "currency": "USD"}
        if path == "query": return {"status": "SUCCESS", "results": [{"url": "https://audio.example.test/a.mp3"}, {"url": "http://bad.test/b.mp3"}]}
        self.submits += 1
        if self.fail_submit: raise TimeoutError("provider response lost")
        self.last_payload = body
        return {"taskId": "provider-task"}

    def event(self, request_id="a" * 32, method="POST", **changes):
        body = {"device": "offline-device", "requestId": request_id, "title": "Original song", "duration": 40,
                "tempo": 120, "genre": "K-pop", "mood": "明亮", "instrumental": True}
        body.update(changes)
        return {"httpMethod": method, "path": "/music" if method == "POST" else "/music/" + request_id,
                "headers": {"X-Test-Code": "offline-only"}, "body": json.dumps(body), "queryString": {"device": "offline-device"}}

    def value(self, event):
        r = music.route(event); return r["statusCode"], json.loads(r["body"])

    def test_unrelated_routes_untouched(self):
        self.assertIsNone(music.route({"path": "/dress"}))

    def test_missing_private_configuration_is_disabled(self):
        with patch.dict(os.environ, {"MUSIC_TEST_CODE": "", "TEST_CODE": ""}):
            self.assertEqual(self.value(self.event())[0], 503)
        self.assertEqual(self.submits, 0)

    def test_authentication_precedes_any_provider_submission(self):
        e = self.event(); e["headers"] = {}
        self.assertEqual(self.value(e)[0], 403); self.assertEqual(self.submits, 0); self.assertFalse(self.objects)

    def test_official_custom_song_payload_and_40_second_target(self):
        code, r = self.value(self.event())
        self.assertEqual(code, 200); self.assertEqual(r["status"], "running")
        self.assertEqual(self.last_payload["duration"], 40)
        self.assertEqual(self.last_payload["make_instrumental"], "true")
        self.assertNotIn("vocalGender", self.last_payload)
        self.assertEqual(music.MODEL, "rhart-audio/suno-v6/custom")

    def test_japanese_vocal_keeps_user_lyrics(self):
        self.value(self.event(instrumental=False, lyrics="[Chorus]\n君と歌おう"))
        self.assertEqual(self.last_payload["vocalGender"], "f")
        self.assertEqual(self.last_payload["prompt"], "[Chorus]\n君と歌おう")

    def test_repeated_request_submits_only_once(self):
        self.value(self.event()); self.value(self.event()); self.assertEqual(self.submits, 1)

    def test_changed_payload_cannot_reuse_a_request(self):
        self.value(self.event())
        self.assertEqual(self.value(self.event(title="Changed"))[0], 409); self.assertEqual(self.submits, 1)

    def test_lost_provider_response_never_auto_submits_again(self):
        self.fail_submit = True
        self.assertEqual(self.value(self.event())[0], 502)
        self.assertEqual(self.value(self.event())[0], 200)
        self.assertEqual(self.submits, 1)

    def test_price_cap_blocks_paid_call(self):
        self.quote = .11
        self.assertEqual(self.value(self.event())[0], 503); self.assertEqual(self.submits, 0); self.assertFalse(self.objects)

    def test_daily_limit_blocks_third_generation(self):
        self.value(self.event("a" * 32)); self.value(self.event("b" * 32))
        self.assertEqual(self.value(self.event("c" * 32))[0], 429); self.assertEqual(self.submits, 2)

    def test_query_restores_results_without_another_paid_call(self):
        self.value(self.event()); code, r = self.value(self.event(method="GET"))
        self.assertEqual(code, 200); self.assertEqual(r["status"], "done")
        self.assertEqual(len(r["tracks"]), 1); self.assertEqual(self.submits, 1)

    def test_bad_duration_does_not_create_task(self):
        self.assertEqual(self.value(self.event(duration=360))[0], 400); self.assertEqual(self.submits, 0)

    def test_global_slots_limit_different_devices(self):
        with patch.dict(os.environ, {"MUSIC_GLOBAL_LIMIT": "2"}):
            self.value(self.event("a" * 32, device="one"))
            self.value(self.event("b" * 32, device="two"))
            self.assertEqual(self.value(self.event("c" * 32, device="three"))[0], 429)
        self.assertEqual(self.submits, 2)

    def test_parallel_device_claims_never_exceed_two_slots(self):
        events = [self.event(("%032x" % i)) for i in range(1, 13)]
        with ThreadPoolExecutor(max_workers=8) as workers:
            results = list(workers.map(self.value, events))
        self.assertEqual(sum(code == 200 for code, _ in results), 2)
        self.assertEqual(self.submits, 2)

    def test_parallel_global_claims_never_exceed_global_limit(self):
        events = [self.event(("%032x" % i), device="device-%s" % i) for i in range(1, 17)]
        with patch.dict(os.environ, {"MUSIC_GLOBAL_LIMIT": "3"}):
            with ThreadPoolExecutor(max_workers=8) as workers:
                results = list(workers.map(self.value, events))
        self.assertEqual(sum(code == 200 for code, _ in results), 3)
        self.assertEqual(self.submits, 3)

    def test_lost_global_capacity_keeps_device_reservation(self):
        with patch.dict(os.environ, {"MUSIC_GLOBAL_LIMIT": "1"}):
            self.assertIsNotNone(music._quota("one", "request-one"))
            self.assertIsNone(music._quota("two", "request-two"))
        keys = [key for key in self.objects if "/devices/two/" in key]
        self.assertEqual(len(keys), 1)
        self.assertEqual(self.objects[keys[0]]["request"], "request-two")

    def test_cos_create_uses_signed_forbid_overwrite_and_private_acl(self):
        class Response:
            headers = {"ETag": '"offline"'}
            def __enter__(self): return self
            def __exit__(self, *_): return False
            def read(self): return b""
        with patch.dict(os.environ, {"COS_BUCKET": "offline", "COS_REGION": "ap-test",
                                   "COS_SECRET_ID": "dummy", "COS_SECRET_KEY": "dummy"}):
            with patch.object(music.urllib.request, "urlopen", return_value=Response()) as opening:
                self.real_cos("PUT", "claim.json", {"request": "one"}, create=True)
        request = opening.call_args[0][0]
        headers = {k.lower(): v for k, v in request.header_items()}
        self.assertEqual(headers["x-cos-forbid-overwrite"], "true")
        self.assertEqual(headers["x-cos-acl"], "private")
        self.assertIn("host;x-cos-acl;x-cos-forbid-overwrite", headers["authorization"])
        self.assertNotIn("if-match", headers); self.assertNotIn("if-none-match", headers)

    def test_unsupported_etag_precondition_is_rejected_without_io(self):
        with patch.object(music.urllib.request, "urlopen") as opening:
            with self.assertRaises(ValueError):
                self.real_cos("PUT", "claim.json", {}, etag='"stale"')
        opening.assert_not_called()


if __name__ == "__main__":
    unittest.main()
