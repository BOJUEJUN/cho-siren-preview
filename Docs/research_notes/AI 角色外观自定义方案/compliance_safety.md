# AI 角色外观自定义：法律、平台与内容安全合规（研究笔记，截至 2026-11-03）

> Not legal advice. Research performed 2026-11-03 via web search and primary-source fetches where possible. Items marked **[unverified this session]** come from background knowledge or secondary commentary and must be checked against the primary text before relying on them. Feature under analysis: players type prompts (and possibly upload reference images) → a third-party image model generates a new outfit/look for adult-presented anime female idol characters → result saved in-game and possibly shared. Distribution: Web, iOS, Google Play/Android, Windows, macOS (Steam likely); mainland China + global.

## Q1. Mainland China: what does a game that only calls a third-party filed model still need to do?

### Takeaway
Calling an already-filed model (e.g., Doubao/Seedream, Qwen) does NOT exempt the game: the AI feature must be **registered (登记) with the local (provincial) CAC**, must publicly display the model name + filing/launch number, and the game operator is itself a "provider" under the Interim Measures (API-based provision is explicitly included), carrying content-producer, labeling (explicit + implicit metadata, GB 45438-2025, in force since 2025-09-01), complaint-handling, minors and user-agreement obligations.

### Cited Findings

**Filing vs. registration (备案 vs 登记)**
- CAC announcement (2026-01-12): "对于通过API接口或其他方式直接调用已备案模型能力的生成式人工智能应用或功能，由地方网信办开展登记" — i.e., apps/features that call a filed model via API register with the **local** CAC, not national filing. — [The Paper, CAC 2025 filing announcement](https://www.thepaper.cn/newsdetail_forward_32368980)
- Same announcement: live apps/features "应在显著位置或产品详情页面公示所使用已备案或登记生成式人工智能服务情况", including model name and filing number or launch number (上线编号). — [The Paper](https://www.thepaper.cn/newsdetail_forward_32368980)
- Scale: as of 2025-12-31, 748 generative AI services filed and 435 apps/functions registered (446 and 330 added in 2025). — [The Paper](https://www.thepaper.cn/newsdetail_forward_32368980). A later aggregator figure says 1,112 filed / 731 registered as of 2026-08-31 (search snippet only, not fetched; treat as indicative). — search result referencing [ithome](https://www.ithome.com/0/950/045.htm)
- Services with "舆论属性或者社会动员能力" do filing/registration through the local CAC. — [The Paper](https://www.thepaper.cn/newsdetail_forward_32368980)
- Exact materials, timelines and forms for 登记 were not found in public sources; must be confirmed with the provincial CAC. — (search summary of [ithome](https://www.ithome.com/0/823/347.htm), [bjnews](https://m.bjnews.com.cn/detail/1789387872129240.html))

**《生成式人工智能服务管理暂行办法》 (effective 2023-08-15) — obligations that land on the game**
- Art. 22: "provider" includes those "通过提供可编程接口等方式提供生成式人工智能服务的组织、个人" — wrapping a third-party API into a player-facing feature makes the game a provider. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 2: applies to services offered to the public within mainland China. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 4: no prohibited content; respect IP and commercial ethics; protect others' 肖像权、名誉权、隐私权 and personal information. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 9: provider bears **网络信息内容生产者** responsibility and must sign a service agreement with registered users. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 10: publish target users/uses; take measures against minors' over-reliance/addiction. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 11: collect minimum personal info; do not unlawfully retain inputs/usage records that identify users; honor access/copy/correction/deletion requests. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 12: label generated images/video per deep-synthesis rules. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 14: on illegal content — stop generation/transmission, remove, optimize the model, report to authorities; for violating users — warn, restrict, suspend/terminate, keep records, report. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 15: accessible complaint/report channel with published procedures and deadlines and feedback. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Art. 17: services with public-opinion/social-mobilization attributes need security assessment and algorithm filing. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- The Interim Measures do not themselves contain an explicit real-name mandate (Art. 9 refers to registered users). — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)

**《人工智能生成合成内容标识办法》 (国信办通字〔2025〕2号, effective 2025-09-01) + GB 45438-2025**
- Art. 4(3): images require "在图片的适当位置添加显著的提示标识" (prominent explicit label); downloads/exports must retain explicit labels (Art. 4). — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 5: implicit label in file metadata containing "生成合成内容属性信息、服务提供者名称或者编码、内容编号等制作要素信息"; digital watermarks encouraged. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 6: content-distribution platforms (i.e., if the game lets players share/publish looks, it is also a 传播平台) must detect/verify implicit labels and add prompts. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 7: app distribution platforms (app stores) must ask at listing whether the app provides AI generation and verify labeling materials — expect Chinese Android stores / App Store CN to request this. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 8: user service agreement must explain labeling methods/formats and prompt users to read them. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 9: unlabeled output only on user request with user's written acceptance of labeling responsibility; keep logs incl. recipient info **≥ 6 months**. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 10: no one may maliciously delete/alter/forge/conceal labels or help others do so. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- Art. 12: provide labeling materials during algorithm filing / security assessment. — [CAC text](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm)
- GB 45438-2025 is a **mandatory** national standard effective 2025-09-01, applying to generation service providers and content-distribution service providers; defines explicit label (text/sound/graphic perceivable by users) and implicit label incl. "文件元数据隐式标识". — [PwC China briefing](https://www.pwccn.com/zh/tmt/method-identifying-synthetic-content-generated-ai-sep2025.pdf); [Sidley translation/summary](https://www.sidley.com/en/-/media/resource-pages/ai-monitor/20250314-china-standard-gb-454382025-labeling-method-for-content-generated-by-ai.pdf?la=en); [Guangming](https://m.gmw.cn/2025-03/15/content_1303991695.htm)
- TC260 has issued practice guides on file-metadata implicit labels (6 guides incl. text files, draft). Exact metadata field names/encoding were not retrieved this session. — [secrss](https://www.secrss.com/articles/80110)

### Inferences
- Practical China checklist for a "calls-a-filed-model" game:
  1. Contract only with a model whose 备案号 is published; obtain the vendor's filing number, model name, and labeling/safety commitments in the contract.
  2. Apply for **local CAC 登记** for the "AI 换装" feature before launch (Shanghai/Beijing/Guangdong etc. per company domicile); then display "本功能使用 XX 大模型（备案号/上线编号）" on the feature screen or product detail page.
  3. Explicit label on every generated image (e.g., corner "AI生成" mark) that survives in-game save, screenshot-export and share; plus implicit metadata (provider name/code, content ID, AIGC attribute) written into exported PNG/JPG; consider invisible watermark. If outputs are only rendered inside the game client and never exported as files, labeling in the UI (persistent "AI 生成" badge on the outfit) is the minimum; any export/share path needs both labels.
  4. If sharing exists: implement platform-side detection of implicit labels (Art. 6) and show "AI 生成" prompts on shared posts.
  5. User agreement + privacy policy updates: labeling explanation (Art. 8), content-producer responsibility, prohibited uses, complaint channel with deadlines (Art. 15), log retention ≥ 6 months for unlabeled-output requests (simplest: never offer unlabeled output).
  6. Prompt/output moderation with removal, model-tuning feedback, user warning/ban ladder and reporting to authorities (Art. 14); keep records.
  7. Minors: restrict or disable the AI feature for under-18 accounts (inference; see Gaps on specific rules).
- Third-party vendor's own filtering does not discharge the game's Art. 9 content-producer duty — the game should run its own prompt and output checks.

### Gaps
- **深度合成规定** (《互联网信息服务深度合成管理规定》, effective 2023-01-10): not fetched this session. From background knowledge [unverified this session], Art. 9 requires real-identity authentication of users (mobile number / ID / national network identity) and Art. 17 requires labeling; also algorithm filing for deep-synthesis service providers and "technical supporters". Verify at the CAC site (expected URL https://www.cac.gov.cn/2022-12/11/c_1672221949354811.htm). In practice, a CN game already enforces real-name + anti-addiction via the NPPA system, which likely satisfies the identity piece.
- Whether a game calling an API must also do **算法备案 (深度合成服务提供者)** in addition to 生成式AI 登记 — practice reportedly varies by province; no authoritative source found.
- **版号 implications**: no NPPA rule found on whether adding an AIGC feature triggers 版号变更 approval or affects approval. Searches returned only general 版号 info ([moegirl](https://zh.moegirl.org.cn/%E7%89%88%E5%8F%B7)) and approval-cadence reports ([dfcfw research PDF](https://pdf.dfcfw.com/pdf/H3_AP202609071829103352_1.pdf)). Inference: a material new content-generating system plausibly counts as substantial content change and NPPA reviewers may be wary of uncontrolled UGC imagery; confirm with the publishing unit (出版单位) before shipping in CN. High uncertainty.
- **Minors protection specifics** (《未成年人网络保护条例》 effective 2024-01-01; NPPA 2021 gaming time-limit notice): not fetched this session; no generative-AI-specific minors rule located.
- Exact metadata schema (GB 45438-2025 Annex) not retrieved.

## Q2. Content safety: prompt filtering, output moderation, available APIs, cost/latency

### Takeaway
All jurisdictions and stores require filtering before display/sharing plus a report channel; for this product the highest-risk categories are sexualized output, minor-looking bodies, real-person likeness, and third-party IP/brands. Use layered defense (prompt allow-list/templating → vendor safety settings → independent output image moderation → human review queue for shared content). Pricing/latency for Chinese moderation APIs was not publicly retrievable this session.

### Cited Findings
- Google Play restricted examples for AI generators include "AI-generated non-consensual deepfake sexual material" and "Generative AI applications primarily intended to be sexually gratifying", plus content that may exploit or abuse children, bullying/harassment. — [Google Play AI-Generated Content policy](https://support.google.com/googleplay/android-developer/answer/14094294)
- Apple 1.2 names "intellectual property infringement, nonconsensual intimate imagery, and anonymous bullying" as the core UGC risks and requires a filtering method for objectionable material. — [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- Apple 1.1.4 bans "Overtly sexual or pornographic material". — [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- Steam: games with live-generated AI content must describe the guardrails that prevent illegal output, and live-generated adult-only sexual content is not allowed. — [tbreak](https://tbreak.com/steam-ai-disclosure-policy-update-developers/); [the-decoder](https://the-decoder.com/valve-tightens-review-process-for-games-with-generative-ai-content-on-steam/) (secondary sources)
- China Interim Measures Art. 4 & 14: protect portrait rights; stop/remove illegal content and report. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- 阿里云内容安全 图片审核增强版 2.0: AIGC scenario detection — whether AIGC images contain violations, **"是否含有疑似侵权的元素"**, re-photography, and whether an image is AIGC-generated; AIGC-detection regions Beijing/Shanghai only; Qwen-based VL moderation (postImageCheckByVL) covering 色情、性感、涉政、暴恐、违禁 etc.; billing pay-as-you-go with prepaid packs. — [Alibaba Cloud docs](https://www.alibabacloud.com/help/zh/content-moderation/latest/image-audit-enhanced-edition-detects-aigc-infringement); [help.aliyun.com](https://help.aliyun.com/document_detail/2878282.html)

### Inferences
- Recommended pipeline for this game:
  1. **Constrain input**: structured outfit builder (garment type, color, material, style tags) + free text limited in length; block-list for sexual terms, age terms (loli, 萝莉, child, 学生 + sexual context), real names (celebrity list), brand/IP names (other games, luxury logos). Inject fixed safety prompt ("adult, fully clothed, no nudity") and negative prompts server-side; never let the client call the model directly.
  2. **Fix the identity**: use image-edit/inpainting on the existing adult character art so body proportions/age presentation stay locked; only the clothing region is regenerated. This addresses minor-looking drift and keeps the "homepage character art" contract intact.
  3. **Output moderation**: run every output through an independent classifier (Aliyun/NetEase Yidun/Shumei/Tencent for CN; e.g., OpenAI omni-moderation or Google Cloud Vision SafeSearch for global) for porn/sexy, minors, logo/brand detection, celebrity face; set "sexy" threshold strictly because idol outfits are borderline already.
  4. **Sharing gate**: private-by-default; shared looks go through stricter thresholds + sampled human review; in-app report button on every shared look; block user; ban ladder.
  5. Store prompts/outputs/moderation decisions ≥ 6 months (CN Art. 9 logs) with privacy minimization.
- Since the characters wear "sometimes revealing" outfits, expect frequent borderline "性感" classifications; define a written policy for what is acceptable (e.g., swimsuit allowed, no lingerie/see-through/sexualized poses) and tune per region (CN stricter).

### Gaps
- No current per-1,000-image prices or latency figures found for 阿里云、网易易盾、数美、腾讯天御 (pricing pages not retrieved; Aliyun only confirmed pay-as-you-go + packs). Must be obtained from console/quotes.
- OpenAI moderation endpoint (background knowledge: omni-moderation model accepts images and is free of charge) and Google Imagen/Gemini safety settings (safety filter level, person-generation controls) **[unverified this session]** — check https://platform.openai.com/docs/guides/moderation and Google Cloud Vertex AI docs.
- Doubao/Seedream and Qwen built-in output filtering specifics not researched.

## Q3. Copyright / IP: ownership of outputs, reference-image risks, ToS clauses

### Takeaway
China (Beijing Internet Court, 2023) has recognized copyright in an AI image where the user's prompts/parameters/selection showed original intellectual input, while the US denies protection for purely AI-generated material (Copyright Office Part 2, Jan 2025; Thaler cert denied 2026-03-02). For a game, the bigger practical risk is outputs that copy third-party IP/brands or real people; ToS must allocate ownership/licensing to the operator and prohibit infringing inputs.

### Cited Findings
- 北京互联网法院 李某诉刘某 (2023)京0491民初11279号, judgment 2023-11-27: Stable Diffusion image generated via positive/negative prompts, steps, guidance scale, seed was held a 美术作品 with the user as author (原创性 from prompt design, parameter adjustment, selection); AI is not a legal subject; damages ¥500 + apology; not appealed. — [The Paper](https://www.thepaper.cn/newsDetail_forward_25725671); [Guangming](https://m.gmw.cn/2023-11/29/content_1303585783.htm)
- The issue remains contested in Chinese academia. — [The Paper](https://www.thepaper.cn/newsDetail_forward_26011024)
- US Copyright Office Part 2 (Copyrightability), released 2025-01-29: prompts alone do not give sufficient control; human expressive inputs perceptible in output may be protected; creative selection/arrangement protectable; case-by-case, protection does not extend to the AI-generated material itself. — [Holland & Knight](https://hklaw.com/en/insights/publications/2025/01/us-copyright-office-issues-report-on-copyrightability-of-generative-ai); [Sidley](https://sidley.com/en/insights/newsupdates/2025/02/us-copyright-office-issues-report-on-artificial-intelligence-and-copyrightability); [Copyright Alliance](https://copyrightalliance.org/ai-report-part-2-copyrightability/)
- Thaler v. Perlmutter: Supreme Court denied certiorari on 2026-03-02 (No. 25-449), leaving the human-authorship requirement in place. — [SCOTUSblog](https://www.scotusblog.com/cases/case-files/thaler-v-perlmutter); [Supreme Court docket](https://www.supremecourt.gov/docket/docketfiles/html/public/25A82.html)
- Apple 1.2 explicitly lists IP infringement as a UGC risk the developer must police, and makes the developer responsible for removing violating content. — [Apple guidelines](https://developer.apple.com/app-store/review/guidelines/)
- China Interim Measures Art. 4 requires respecting IP and others' portrait rights. — [CAC text](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm)
- Aliyun offers detection of "疑似侵权的元素" in AIGC images. — [Alibaba Cloud docs](https://www.alibabacloud.com/help/zh/content-moderation/latest/image-audit-enhanced-edition-detects-aigc-infringement)

### Inferences
- Ownership: the base characters are the operator's IP; any AI outfit is at best a derivative of operator IP. ToS should state (a) operator owns the character and all in-game assets; (b) to the extent a player acquires any rights in the outfit/prompt, the player grants the operator a worldwide, perpetual, royalty-free, sublicensable license (incl. for promotion and model improvement if desired, with opt-out where required); (c) no guarantee outputs are copyrightable or unique; (d) outputs may resemble other users' outputs.
- Reference image uploads (e.g., a branded dress, another game's character) are the highest-risk path: either disable uploads at launch, or restrict to the player's own drawings with a warranty + indemnity, strip faces, and run logo/IP detection. Upload of real-person photos also triggers personal-information/portrait-rights obligations and Apple 5.1.2(i) consent for sending data to third-party AI.
- ToS/Community rules checklist: prohibited inputs (others' IP, trademarks/logos, real people, sexual/minor content); player warranty they hold rights in uploads; indemnity; notice-and-takedown (DMCA agent for US; 通知-删除 under China's Civil Code) and repeat-infringer policy; AI labeling explanation (CN Art. 8); right to moderate/remove/ban; disclosure of third-party model vendor and data flows.

### Gaps
- A reported 2025 Chinese case (张家港 court) holding an AI image NOT protectable for lack of evidence of creative input was not found in search results; other 2024–2026 Chinese AI-copyright rulings (e.g., Changshu court, Guangzhou Internet Court Ultraman case on platform liability) were not verified this session. The Guangzhou Internet Court Ultraman case (2024) — platform held liable for generating infringing images of a known character — is directly relevant to "copying other games' outfits/characters" **[unverified this session]**.
- No case law found specific to fashion-brand/outfit copying via AI in games.

## Q4. App store & platform policies + EU AI Act Article 50

### Takeaway
Apple now explicitly treats "AI content creation apps" as UGC (filtering, reporting, blocking, contact info) and requires consent before sending personal data to third-party AI; Google Play requires in-app reporting/flagging of offensive AI output and bans sexually-gratifying gen-AI apps; Steam (rewritten Jan 2026) requires a live-generated AI disclosure describing guardrails and bans live-generated adult sexual content; EU AI Act Art. 50 applies from 2026-08-02, with a short grace period to 2026-12-02 for machine-readable marking only for systems already on the market.

### Cited Findings

**Apple**
- 1.2 (verbatim): applies to apps that let users "create, generate, upload, share" content, "This includes social media, user-generated content platforms, and AI content creation apps", requiring: filtering of objectionable material, reporting with timely response, ability to block abusive users, published contact information. Apps used primarily for pornographic content or objectification of real people may be removed. — [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 1.2.1(a): creator apps must identify content exceeding the app's age rating and use an age-restriction mechanism based on verified or declared age. — [Apple guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 5.1.2(i): "You must clearly disclose where personal data will be shared with third parties, including with third-party AI, and obtain explicit permission before doing so." — [Apple guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 2.3.6: answer age rating questions honestly; developer responsible for local rating requirements. — [Apple guidelines](https://developer.apple.com/app-store/review/guidelines/)
- Age ratings overhaul (July 2025): new 13+, 16+, 18+ tiers alongside 4+ and 9+; new questions on in-app controls, capabilities, medical/wellness, violent themes; answers required by 2026-01-31 to keep submitting updates. — [Apple Developer News](https://developer.apple.com/news/?id=ks775ehf); [9to5mac](https://9to5mac.com/?p=1011484); [PocketGamer.biz](https://www.pocketgamer.biz/apple-overhauls-app-store-age-ratings-with-new-categories)
- A secondary blog (dated 2026-09-29) claims the questionnaire changed again and responses are required for submissions from September 2026; not verified on Apple's site. — [trysonar](https://trysonar.app/blog/app-store-age-rating)
- Texas SB2420 (App Store Accountability Act) may require additional parental-consent handling for apps updated after 2026-01-01 (secondary mention). — [trysonar](https://trysonar.app/blog/app-store-age-rating)

**Google Play**
- AI-Generated Content policy covers "Text-to-image, voice-to-image, and image-to-image apps that use AI to generate images"; restricted content includes non-consensual deepfake sexual material and gen-AI apps "primarily intended to be sexually gratifying". — [Google Play policy help](https://support.google.com/googleplay/android-developer/answer/14094294)
- Requirement (announced Oct 2023, effective early 2024): apps must let users report/flag offensive AI-generated content **without leaving the app**, and developers should use reports to inform filtering. — [Google Play Console help 13985936](https://support.google.com/googleplay/android-developer/answer/13985936?hl=en); [TechCrunch](https://techcrunch.com/?p=2619733); [9to5Google](https://9to5google.com/2023/10/25/google-play-gen-ai-policy/)

**Steam (Windows/macOS via Steam)**
- Mid-January 2026 rewrite of the AI disclosure in the Content Survey: Pre-Generated vs Live-Generated content; live-generated requires describing guardrails against illegal output; player reporting of illegal live-generated content through the Steam overlay; disclosure shown on the store page; dev-tooling (e.g., coding assistants) excluded. — [tbreak](https://tbreak.com/steam-ai-disclosure-policy-update-developers/); [VGC](https://www.videogameschronicle.com/news/steam-pages-will-now-disclose-which-games-use-ai-generated-content/); [PCGamesN](https://www.pcgamesn.com/steam/ai-content-reporting) (secondary; primary Steamworks text not fetched)
- Live-generated adult-only sexual content is not currently allowed on Steam. — [the-decoder](https://the-decoder.com/valve-tightens-review-process-for-games-with-generative-ai-content-on-steam/)

**EU AI Act Article 50**
- Regulation (EU) 2024/1689 Art. 50(2): providers of AI systems generating synthetic image content must mark outputs in a machine-readable format detectable as AI-generated; Art. 50(4): deployers of deep fakes must disclose, with lighter treatment for evidently artistic/creative/fictional works. — [EUR-Lex Regulation 2024/1689](https://eur-lex.europa.eu/eli/reg/2024/1689/oj) (text not re-fetched this session; wording per background knowledge — verify)
- Art. 50 applies from **2026-08-02**; the Digital Omnibus (AI Omnibus) adopted a transitional period moving Art. 50(2) marking to **2026-12-02** only for systems already placed on the market before 2026-08-02; new systems get no grace period; Art. 50(1) unchanged. — [CSA research note](https://labs.cloudsecurityalliance.org/research/csa-research-note-eu-ai-act-article50-watermarking-deadline/); [iubenda](https://www.iubenda.com/en/?p=222226); [Dastra](https://www.dastra.eu/en/blog/ai-act-transparency-rules-what-changes-august-2-2026/60164)
- Commission published a draft Code of Practice on AI labelling/transparency (Jan 2026); reports say it was finalized ~June 2026 and signed by ~190 organizations; Commission also adopted Art. 50 Guidelines. Voluntary but a compliance route. — [Jones Day](https://jonesday.com/en/insights/2026/01/european-commission-publishes-draft-code-of-practice-on-ai-labelling-and-transparency); [MoFo](https://www.mofo.com/resources/insights/260911-full-transparency-organizations-sign-eu-code); [Stephenson Harwood](https://perspectives.stephensonharwood.com/post/102nfqh/eu-ai-act-update-european-commission-adopts-guidelines-on-article-50-transparenc). Sources differ on finalization timing.

### Inferences
- Platform checklist:
  - **iOS/macOS App Store**: in-app filter + report button per image + block user + contact info in app & store page; explicit consent dialog before sending any player data/uploads to the AI vendor (name the vendor); re-answer age-rating questionnaire (UGC/AI generation, sexual/suggestive themes → likely 16+ or 18+ for global; mild suggestive themes may land 12+/13+ depending on answers); App Review notes describing moderation pipeline.
  - **Google Play**: in-app reporting for AI outputs (not just shared posts), Data safety form update for data sent to AI vendor, IARC questionnaire update (user interaction/UGC); keep away from "sexually gratifying" positioning in store listing.
  - **Steam**: update Content Survey → Live-Generated AI, describe guardrails; ensure no adult-only sexual output; the overlay report path will exist—make sure the team monitors it.
  - **Web**: same filters; EU/China labeling applies equally.
  - **EU**: since the feature would launch after 2026-08-02, no grace period — machine-readable marking (C2PA/metadata + watermark) required at launch; visible "AI-generated" label is good practice and aligns with China's explicit label anyway. The game, by integrating a third-party model into its own system under its own brand, is likely an Art. 50(2) "provider" of the AI system (inference; regulatory characterization uncertain).
- One implementation (visible "AI生成/AI-generated" badge + metadata with C2PA manifest + vendor watermark + provider code/content ID) can satisfy CN GB 45438-2025 and EU Art. 50(2) together; verify that CN metadata fields and C2PA can coexist in the same file.

### Gaps
- Primary Steamworks documentation text and exact Content Survey questions not fetched.
- Whether Apple's 2026 questionnaire revision adds an explicit "AI-generated content" question could not be verified.
- Google Play "Best Practices to Safeguard AI-Generated Content" page content not retrieved; Google's policy page shows no update date.
- Korea AI Basic Act (effective 2026-01-22), Japan, US state laws (e.g., California AI Transparency Act SB 942) not researched; may matter for "global players".
- GPAI provider obligations of the upstream model vendor (EU Art. 53) and whether Chinese-only models (Doubao/Qwen China region) can lawfully serve EU/global players (data transfer under PIPL) not researched — likely need a separate global model vendor for non-CN regions.
