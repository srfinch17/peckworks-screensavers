"""
Makes a DEPTH MAP of a photo: a grey picture the same shape, where white means
"near the camera" and black means "far away". A photo saver uses it to put
moving things IN the scene rather than on top of it: a firefly is hidden
wherever the photo is nearer than the firefly is.

It runs once, on your machine, while building a saver; nothing here goes
inside the screensaver. The work is done by a free AI model, Depth Anything V2
Small (Apache 2.0 licence), which looks at one picture and guesses how far away
each part is, the way you can tell the near grass from the far trees with one
eye shut.

Setup (once):
    python -m venv C:\\Temp\\pd
    C:\\Temp\\pd\\Scripts\\python -m pip install opencv-python numpy onnxruntime
    curl -L -o dav2s.onnx https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/main/onnx/model.onnx

Use:
    C:\\Temp\\pd\\Scripts\\python scripts\\depthmap.py dav2s.onnx src\\CotswoldBrook\\brook.jpg src\\CotswoldBrook\\brook_depth.png
"""
import sys
import cv2
import numpy as np
import onnxruntime as ort

model, photo, out = sys.argv[1:4]
OUT_WIDTH = 1920       # plenty: depth changes slowly, and the saver smooths it when it enlarges
SHORT_SIDE = 770       # the size the model looks at; bigger = sharper edges, slower (seconds, once)

img = cv2.imread(photo)
h, w = img.shape[:2]

# The model reads the picture in 14-pixel squares, so both sides must be a multiple of 14.
s = SHORT_SIDE / min(h, w)
iw, ih = round(w * s / 14) * 14, round(h * s / 14) * 14
x = cv2.resize(cv2.cvtColor(img, cv2.COLOR_BGR2RGB), (iw, ih), interpolation=cv2.INTER_AREA).astype(np.float32) / 255
x = (x - [0.485, 0.456, 0.406]) / [0.229, 0.224, 0.225]          # the colour scaling the model was trained with
x = x.transpose(2, 0, 1)[None].astype(np.float32)                 # rows x columns x colours  ->  colours x rows x columns

sess = ort.InferenceSession(model, providers=["CPUExecutionProvider"])
d = sess.run(None, {sess.get_inputs()[0].name: x})[0].squeeze()

# The answer is "nearness" (bigger = nearer), in no particular units. Stretch it to 0..255.
d = (d - d.min()) / (d.max() - d.min())
d = cv2.resize(d, (OUT_WIDTH, round(h * OUT_WIDTH / w)), interpolation=cv2.INTER_CUBIC)
cv2.imwrite(out, np.clip(d * 255, 0, 255).astype(np.uint8))
print("wrote", out)
