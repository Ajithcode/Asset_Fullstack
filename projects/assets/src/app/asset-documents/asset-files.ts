/**
 * GET /api/asset-registrations/{assetRegistrationId}/files: the documents and images of one asset in one response.
 * The response is read leniently: documents / images arrays (or a single files list split by kind, content type or
 * extension), with the field names of /api/asset-documents and /api/asset-images or plainer ones (name, url, size, ...).
 */
type Raw = Record<string, unknown>;

const IMAGE_EXT = /\.(png|jpe?g|gif|webp|bmp|svg|tiff?|heic|avif)(\?|#|$)/i;

const isObj = (v: unknown): v is Raw => !!v && typeof v === 'object' && !Array.isArray(v);
const list = (v: unknown): Raw[] => (Array.isArray(v) ? v.filter(isObj) : []);

/** First of `keys` holding a non-empty string (numbers are turned into strings). */
export function str(o: Raw, ...keys: string[]): string | null {
  for (const k of keys) {
    const v = o[k];
    if (typeof v === 'string' && v.trim()) return v;
    if (typeof v === 'number' && Number.isFinite(v)) return String(v);
  }
  return null;
}

/** First of `keys` holding a finite number (numeric strings count). */
export function num(o: Raw, ...keys: string[]): number | null {
  for (const k of keys) {
    const v = typeof o[k] === 'string' ? Number(o[k]) : o[k];
    if (typeof v === 'number' && Number.isFinite(v)) return v;
  }
  return null;
}

/** First of `keys` holding an array of strings. */
export function strList(o: Raw, ...keys: string[]): string[] | null {
  for (const k of keys) {
    const v = o[k];
    if (Array.isArray(v)) return v.filter((x): x is string => typeof x === 'string');
    if (typeof v === 'string' && v.trim()) return v.split(',').map((x) => x.trim()).filter(Boolean);
  }
  return null;
}

function isImage(f: Raw): boolean {
  const kind = (str(f, 'kind', 'fileKind', 'fileCategory', 'category', 'fileType', 'type', 'source') ?? '').toLowerCase();
  if (kind.includes('image') || kind.includes('photo')) return true;
  if (kind.includes('document')) return false;
  const ct = (str(f, 'contentType', 'mimeType') ?? '').toLowerCase();
  if (ct) return ct.startsWith('image/');
  return IMAGE_EXT.test(str(f, 'fileName', 'fileUrl', 'url', 'name') ?? '');
}

/** Splits a /files response into raw document and image records. */
export function splitAssetFiles(res: unknown): { documents: Raw[]; images: Raw[] } {
  const body = isObj(res) && isObj(res['data']) ? (res['data'] as Raw) : res;
  if (isObj(body)) {
    const documents = list(body['documents'] ?? body['assetDocuments']);
    const images = list(body['images'] ?? body['assetImages']);
    if ('documents' in body || 'images' in body || 'assetDocuments' in body || 'assetImages' in body) return { documents, images };
  }
  const files = Array.isArray(body) ? list(body) : isObj(body) ? list(body['files'] ?? body['items'] ?? body['data']) : [];
  return { documents: files.filter((f) => !isImage(f)), images: files.filter(isImage) };
}
