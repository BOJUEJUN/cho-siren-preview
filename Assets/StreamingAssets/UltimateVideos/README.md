# Optional original-character ultimate clips

No clips have been supplied. No video is fabricated, generated or downloaded by this implementation.
The existing original-character portrait cut-in remains the default.

Once the owner supplies an approved original-character MP4, put it under this folder and explicitly
add its entry to `Assets/Resources/UltimateVideos/manifest.json`:

```json
{
  "entries": [
    {
      "characterId": "exact-unit-definition-id",
      "path": "that-character/finale.mp4",
      "approvedOriginalArt": true,
      "durationSeconds": 1.3
    }
  ]
}
```

The ID must match the caster. Paths are relative to this local folder; remote URLs and traversal
are rejected. The preview is confined to the central stage and uses silent video so unknown audio
does not override the selected game music. It never changes damage, target, energy, or cooldowns.
An absent mapping/file, decode error, or preparation taking over 0.7 seconds falls back to the
existing portrait accent. The maximum clip accent is 1.4 seconds. Closing/pausing cancels it.
