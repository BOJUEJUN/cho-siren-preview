# ComfyUI workflows

Each brief names a workflow file here (`i2v.json`, `i2v-firstlast.json`, `t2i.json` …).
Build the graph in ComfyUI, then **Workflow → Export (API)** and save it here under that name.

In the exported JSON, set these string values exactly (the factory replaces them on every run):

| placeholder | where |
|---|---|
| `"{{prompt}}"` / `"{{negative}}"` | positive / negative text encode node `text` |
| `"{{image}}"` | LoadImage node `image` (the reference / first frame) |
| `"{{last_image}}"` | second LoadImage for the last frame (first/last-frame models; seamless loops) |
| `"{{seed}}"` | sampler `seed` / `noise_seed` |
| `"{{width}}"` `"{{height}}"` `"{{frames}}"` `"{{fps}}"` | latent / video size, length, frame rate, video combine fps |

The output node must save to ComfyUI's output folder (SaveVideo / VHS_VideoCombine / SaveImage), not preview only.
