"""Render the project's original modular pawn drawings (PyMuPDF and Pillow).

Run with Python from any directory. Every layer shares one 512 px frame.
Optional --proof PATH writes a contact sheet outside the game assets.
"""

import argparse
import io
from pathlib import Path

import fitz
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/Game/Content/Art/Generated/PawnLayers"
SIZE = 512
INK = "#241e16"
SHAPES = ("slim", "broad", "round")
DIRECTIONS = ("front", "back", "side")
HAIRSTYLES = ("short", "bob", "bun")
COLORS = {
    "skin": ("#f2cca3", "#e7b58a"),
    "body": ("#cdb99b", "#b79e80"),
    "shirt": ("#84804c", "#64643d"),
    "jacket": ("#975c35", "#794629"),
    "hair": ("#4c3b2d", "#362a22"),
}


def path(d, fill, weight=8):
    stroke = INK if weight else "none"
    color = COLORS[fill][0] if fill in COLORS else fill
    return (f'<path d="{d}" fill="{color}" stroke="{stroke}" stroke-width="{weight}" '
            'stroke-linejoin="round" stroke-linecap="round"/>')


def render(key, paths):
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512">{paths}</svg>'
    # Supersampling keeps the ink clean when the Player reduces it to a few dozen pixels.
    with fitz.open(stream=svg.encode(), filetype="svg") as source:
        bitmap = source[0].get_pixmap(matrix=fitz.Matrix(2, 2), alpha=True)
        with Image.open(io.BytesIO(bitmap.tobytes("png"))) as raster:
            picture = np.array(raster.resize((SIZE, SIZE), Image.Resampling.LANCZOS))
    # Keep the ink at one value and shade only the painted land inside it.
    material = key.split(".")[1]
    light, dark = COLORS["skin" if material == "face" else material]
    light_rgb, dark_rgb, ink = (np.array(list(bytes.fromhex(color[1:])), dtype=float)
                                for color in (light, dark, INK))
    y, x = np.mgrid[:SIZE, :SIZE]
    gradient = (.35 * x + .65 * y) / SIZE
    paint = np.clip((picture[:, :, 0].astype(float) - ink[0]) / (light_rgb[0] - ink[0]), 0, 1)
    picture[:, :, :3] = np.clip(picture[:, :, :3] + (gradient * paint)[:, :, None] * (dark_rgb - light_rgb), 0, 255)
    Image.fromarray(picture).save(DEST / f"{key}.png")


# Each row is the outside of one shoulder, waist, hip and hem, traced top to bottom.
# Values are distances from the spine. Broad has angular shoulders, round has an
# unbroken belly curve, and slim has an inward waist and outward hip.
PROFILES = {
    "slim": ((76, 225, 108, 252, 101, 291), (96, 323, 76, 337, 78, 367),
             (80, 394, 99, 431, 88, 462), (76, 479, 39, 482, 0, 482)),
    "broad": ((85, 227, 148, 251, 151, 284), (153, 312, 122, 332, 115, 354),
              (108, 382, 120, 442, 109, 463), (95, 479, 49, 482, 0, 482)),
    "round": ((97, 227, 144, 281, 154, 343), (164, 387, 156, 427, 136, 451),
              (124, 465, 112, 471, 91, 475), (64, 481, 36, 482, 0, 482)),
}


def contour(shape, direction, sign, reverse=False):
    curves = []
    start = (256 + sign * 43, 218)
    for values in PROFILES[shape]:
        points = []
        for x, y in zip(values[::2], values[1::2]):
            if direction == "side":
                # Right-facing belly and a straighter back keep the profile readable.
                x *= .72 if sign < 0 else .84
            points.append((round(256 + sign * x, 1), y))
        curves.append([start, *points])
        start = points[-1]
    if reverse:
        curves = [curve[::-1] for curve in curves[::-1]]
    return " ".join("C" + " ".join(f"{x} {y}" for x, y in curve[1:]) for curve in curves)


def clothes(shape, direction):
    left = contour(shape, direction, -1)
    right = contour(shape, direction, 1, reverse=True)
    sides = f"M213 218 {left} {right}"
    # The first layer includes the neck. The two clothing necklines reveal it.
    if direction == "front":
        neck = "C294 259 281 273 256 273 C231 273 218 259 213 218"
    elif direction == "back":
        neck = "C291 242 277 250 254 250 C234 250 221 243 213 218"
    else:
        neck = "C297 248 288 267 273 269 C250 271 229 246 213 218"
    result = {
        "body": path(sides + " C284 206 228 206 213 218 Z", "body"),
        "shirt": path(sides + " " + neck + " Z", "shirt"),
    }
    if direction == "front":
        # Two garment fronts never join across the opening. Each has its own hem.
        left_end = left.rsplit("C", 1)[0]
        right_top = contour(shape, direction, 1).rsplit("C", 1)[0]
        jacket = path(f"M213 218 {left_end} Q179 481 218 481 L218 342 Q217 276 200 230 Z", "jacket")
        jacket += path(f"M299 218 {right_top} Q333 481 294 481 L294 342 Q295 276 312 230 Z", "jacket")
        jacket += path("M199 234 L179 262 L208 282 L192 283 L217 321 M313 234 L333 262 L304 282 L320 283 L295 321", "none", 6)
    elif direction == "back":
        jacket = path(sides + " Q286 240 256 241 Q226 240 213 218 Z", "jacket")
        jacket += path("M217 233 Q255 258 294 233", "none", 5)
    else:
        # The opening wraps toward the facing side. The narrow right panel leaves
        # a visible sliver of shirt in profile; the collar is entirely on the coat.
        jacket = path(sides + " Q305 249 308 281 Q317 354 313 452 L313 474 Q295 481 287 481 "
                      "Q286 400 284 352 Q282 289 268 263 Q237 255 213 218 Z", "jacket")
        jacket += path("M267 261 L253 280 L277 293 L264 294 L285 326", "none", 6)
    result["jacket"] = jacket
    return result


FACE = {
    "front": "M254 51 C310 51 341 90 340 151 C339 210 302 246 259 248 C214 249 174 213 171 157 C168 99 196 53 254 51 Z",
    "back": "M255 51 C308 50 342 91 340 151 C340 207 308 239 261 246 C215 245 176 213 171 158 C166 99 200 54 255 51 Z",
    "side": "M244 55 C290 48 324 74 330 115 C333 132 335 149 345 157 Q349 165 339 170 "
            "C339 206 315 235 285 245 C258 252 224 237 206 217 C175 197 164 154 174 113 C184 76 210 59 244 55 Z",
}

EYE_CENTERS = {"front": ((228, 170), (284, 170)), "side": ((309, 165),)}

HAIR = {
    ("short", "front"): "M170 175 Q161 165 162 149 L154 155 Q151 123 162 101 L151 104 Q162 83 177 72 L165 72 "
                        "Q179 52 204 45 L195 40 Q210 28 237 29 L225 21 Q242 17 260 24 Q287 21 311 36 "
                        "L307 29 Q332 38 346 63 L342 63 Q357 88 350 111 L357 109 Q357 134 342 155 "
                        "L337 173 L327 149 Q326 127 316 110 Q300 123 279 130 L281 117 Q265 130 247 135 "
                        "L249 122 Q234 139 215 148 L218 132 Q192 143 178 159 L178 147 Z",
    ("short", "back"): "M166 155 L155 161 Q150 135 159 110 L151 113 Q159 84 178 70 L166 72 "
                       "Q179 53 204 44 L196 39 Q213 26 238 29 L226 21 Q244 17 261 24 Q292 22 315 38 "
                       "L311 31 Q341 43 349 73 Q358 92 350 111 L357 109 Q358 135 344 153 "
                       "L346 162 L337 157 Q332 190 309 209 L312 220 L298 216 L287 232 L280 225 "
                       "Q259 241 249 237 L250 229 L229 236 L225 226 L210 228 L207 215 "
                       "Q182 207 174 187 L168 193 Z",
    ("short", "side"): "M171 179 L161 187 L162 165 L154 171 Q147 144 158 118 L148 123 "
                       "Q152 97 173 76 L162 77 Q177 55 201 44 L193 39 Q208 30 231 30 L222 22 "
                       "Q243 18 262 26 Q288 21 310 40 L312 34 Q338 46 347 73 L342 73 Q353 88 349 106 "
                       "L341 103 L340 123 L326 108 Q307 124 288 129 L291 117 Q266 130 249 155 "
                       "L249 143 Q230 163 222 188 L214 177 L202 207 L194 199 L192 213 Q175 204 171 179 Z",
    ("bob", "front"): "M164 251 Q140 225 146 168 C147 115 164 51 217 32 C267 11 330 30 346 82 "
                      "C361 116 349 162 344 184 Q336 218 347 242 Q324 228 322 183 Q321 130 283 101 "
                      "Q260 129 213 152 C191 165 181 200 191 229 Q194 247 207 262 Q178 253 173 222 Q166 234 164 251 Z",
    ("bob", "back"): "M163 253 Q141 226 146 175 C147 109 164 48 217 32 C272 12 332 34 346 82 "
                     "C360 116 350 171 342 198 Q336 222 346 244 Q327 238 322 220 Q309 242 287 251 "
                     "L287 242 Q250 265 213 255 L218 244 Q196 253 181 238 L177 219 Q168 234 163 253 Z",
    ("bob", "side"): "M170 247 Q144 228 146 187 C145 127 157 59 206 36 C251 13 310 25 336 63 "
                     "Q352 84 346 107 Q334 117 318 122 Q301 134 286 134 L289 123 Q277 137 265 154 "
                     "Q238 195 254 242 Q233 235 225 216 Q224 246 240 264 Q202 259 183 237 Q176 230 175 216 Z",
    ("bun", "front"): "M302 71 C308 28 336 20 354 30 C377 42 381 70 365 90 Q351 108 329 111 Z",
    ("bun", "back"): "M208 98 C199 72 210 45 234 37 C259 28 286 39 296 60 C307 86 298 110 278 122 Q252 138 224 124 Q212 117 208 98 Z",
    ("bun", "side"): "M186 74 C165 49 137 55 131 78 C121 105 139 130 163 130 Q184 133 199 117 Z",
}

BUN_CAP = {
    "front": "M173 156 C156 118 159 79 182 51 C218 13 278 15 315 39 Q360 68 345 127 "
             "Q341 147 336 158 Q330 132 310 119 Q283 100 257 74 Q248 98 214 123 Q185 145 177 164 "
             "Q161 196 174 224 Q146 213 153 184 Q156 166 173 156 Z",
    "back": "M172 166 C155 135 153 86 178 54 C207 17 271 12 311 37 C347 59 359 112 341 161 "
            "Q332 187 300 202 Q269 217 249 231 Q221 224 201 201 Q189 190 177 186 Q169 206 179 222 Q153 213 160 186 Z",
    "side": "M181 180 C155 157 150 109 170 70 C187 32 236 18 280 30 Q327 37 344 78 Q327 98 304 107 "
            "Q264 126 241 146 Q214 164 208 189 Q198 208 206 226 Q177 215 182 191 Z",
}


def proof(destination):
    thumb = 105
    sheet = Image.new("RGB", (thumb * 9, (thumb + 22) * 3), "#cabba3")
    labels = ImageDraw.Draw(sheet)
    for row, shape in enumerate(SHAPES):
        for index, style in enumerate(HAIRSTYLES):
            for column, direction in enumerate(DIRECTIONS):
                keys = [f"body.{shape}", f"shirt.{shape}"]
                if index != 1:
                    keys.append(f"jacket.{shape}")
                keys.extend(["face", f"hair.{style}"])
                composite = Image.new("RGBA", (SIZE, SIZE))
                for key in keys:
                    with Image.open(DEST / f"pawn.{key}.{direction}.png") as part:
                        composite.alpha_composite(part)
                composite.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
                xy = ((index * 3 + column) * thumb, row * (thumb + 22))
                sheet.paste(composite, xy, composite)
                labels.text((xy[0] + 2, xy[1] + thumb), f"{shape} {style} {direction}", fill="#231d14")
    sheet.save(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--proof", type=Path)
    args = parser.parse_args()
    DEST.mkdir(parents=True, exist_ok=True)
    for direction in DIRECTIONS:
        eyes = "".join(
            f'<ellipse cx="{x}" cy="{y}" rx="5.5" ry="8.5" fill="#38281e"/>'
            for x, y in EYE_CENTERS.get(direction, ())
        )
        render(f"pawn.face.{direction}", path(FACE[direction], "skin") + eyes)
        for style in HAIRSTYLES:
            drawing = path(HAIR[style, direction], "hair")
            if style == "bun":
                cap = path(BUN_CAP[direction], "hair")
                drawing = cap + drawing if direction == "back" else drawing + cap
            render(f"pawn.hair.{style}.{direction}", drawing)
        for shape in SHAPES:
            for garment, drawing in clothes(shape, direction).items():
                render(f"pawn.{garment}.{shape}.{direction}", drawing)
    if args.proof:
        proof(args.proof)


if __name__ == "__main__":
    main()
