"""Package the two GPT Image originals. Only cropping and resampling are applied."""
from pathlib import Path
from PIL import Image

assets = Path(__file__).resolve().parents[1] / "assets"
atlas = Image.open(assets / "pip-atlas.png").convert("RGBA")
names = ["stride", "step", "lift", "nap", "biscuit", "scout", "count", "debug", "read"]
x_edges = [0, 411, 830, atlas.width]
y_edges = [0, 405, 810, atlas.height]
for i, name in enumerate(names):
    col, row = i % 3, i // 3
    sprite = atlas.crop((x_edges[col], y_edges[row], x_edges[col + 1], y_edges[row + 1]))
    box = sprite.getchannel("A").point(lambda a: 255 if a >= 14 else 0).getbbox()
    padded = (max(0, box[0] - 2), max(0, box[1] - 2),
              min(sprite.width, box[2] + 2), min(sprite.height, box[3] + 2))
    sprite.crop(padded).save(assets / (name + ".png"), optimize=True)

# Use Pip's face for a legible tray icon, including dedicated small icon sizes.
face = atlas.crop((147, 4, 386, 218))
icon = Image.new("RGBA", (256, 256))
face.thumbnail((240, 230), Image.Resampling.LANCZOS)
icon.alpha_composite(face, ((256 - face.width) // 2, (256 - face.height) // 2))
icon.save(assets / "pip.ico", sizes=[(s, s) for s in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
with Image.open(assets / "habitat-source.png") as plate:
    plate.convert("RGB").resize((1008, 672), Image.Resampling.LANCZOS).save(
        assets / "clearing.jpg", quality=90, optimize=True)
