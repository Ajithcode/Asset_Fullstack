// Builds the white topbar logo (transparent PNG at 1x / 2x / 3x) from public/purple360-logo.jpeg.
// Ink amount (how far a pixel is from white) becomes the alpha of a white pixel, the empty margins are cropped, and each
// size is resized once from the 1600 px original (no CSS filters or blend modes at run time, so edges stay sharp).
// Run: node scripts/make-topbar-logo.mjs      (the topbar shows it at 44 px high, see topbar.scss .topbar-logo)
import sharp from 'sharp';

const SRC = 'public/purple360-logo.jpeg';
const BASE_H = 44;
const { data, info } = await sharp(SRC).removeAlpha().raw().toBuffer({ resolveWithObject: true });
const px = info.width * info.height;
const rgba = Buffer.alloc(px * 4);
let minX = info.width, minY = info.height, maxX = 0, maxY = 0;
for (let i = 0; i < px; i++) {
  const m = Math.min(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
  const a = Math.max(0, Math.min(255, Math.round(((255 - m - 14) * 255) / (255 - 14 - 10))));
  rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = 255;
  rgba[i * 4 + 3] = a;
  if (a > 40) {
    const x = i % info.width, y = (i / info.width) | 0;
    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
  }
}
const pad = 6;
const crop = { left: Math.max(0, minX - pad), top: Math.max(0, minY - pad) };
crop.width = Math.min(info.width, maxX + pad) - crop.left;
crop.height = Math.min(info.height, maxY + pad) - crop.top;
const white = await sharp(rgba, { raw: { width: info.width, height: info.height, channels: 4 } }).extract(crop).png().toBuffer();
for (const [suffix, k] of [['', 1], ['@2x', 2], ['@3x', 3]]) {
  const out = `public/asset360-logo-white${suffix}.png`;
  const r = await sharp(white).resize({ height: BASE_H * k, kernel: 'lanczos3' }).png({ compressionLevel: 9 }).toFile(out);
  console.log(out, r.width + 'x' + r.height);
}
