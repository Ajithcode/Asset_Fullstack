import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';
import { AssetImage, ImageEvent } from '../asset-details/asset-details';
import { AssetLookupService, EventLogDto, toLocalDateTime } from '../asset-documents/asset-lookup.service';

/** Raw shape returned by /api/asset-images (AssetImageResponse). */
interface AssetImageDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  imageName: string;
  imageType: string;
  tags: string[] | null;
  location: string | null;
  remarks: string | null;
  description: string | null;
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number | null;
  width: number | null;
  height: number | null;
  resolution: string | null;
  uploadedBy: string | null;
  uploadDate: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** One stored image file (AssetImageFileRequest). */
interface AssetImageFileRequest {
  imageName: string | null;
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number;
  width: number | null;
  height: number | null;
}

/** Body of POST /api/asset-images: every image is saved once per asset. */
interface CreateAssetImagesRequest {
  assetRegistrationIds: string[];
  images: AssetImageFileRequest[];
  imageType: string;
  tags: string[];
  /** Empty = each asset's own location. */
  location: string | null;
  remarks: string | null;
  description: string | null;
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

/** Body of PUT /api/asset-images/{id}; a null file keeps the current file. */
interface UpdateAssetImageRequest {
  assetRegistrationId: string;
  imageName: string;
  imageType: string;
  tags: string[];
  location: string | null;
  remarks: string | null;
  description: string | null;
  file: AssetImageFileRequest | null;
}

/** An image of the UI backed by an API record. `id` is a local number; `apiId` is the record id. */
export interface ApiImage extends AssetImage {
  apiId: string;
  assetRegistrationId: string;
  assetName: string;
}

/** A local file chosen in the upload dialog, with its name and pixel size. */
export interface ImageUploadItem {
  file: File;
  title: string;
  width: number;
  height: number;
}

/** Shared details of the upload dialog. */
export interface ImageUploadValue {
  type: string;
  tags: string[];
  location: string;
  description: string;
  remarks: string;
}

/** Details of the edit dialog. */
export interface ImageEditValue {
  title: string;
  type: string;
  tags: string[];
  location: string;
  description: string;
  remarks: string;
}

/** Local ids are unique across pages so images can be listed together. */
let nextImageId = 1;

/** Event-log actions as shown in the image history. */
const ACTION_LABELS: Record<string, string> = {
  Created: 'Image uploaded',
  Updated: 'Details edited',
  Deleted: 'Image deleted',
};

function baseHistory(dto: AssetImageDto): ImageEvent[] {
  const events: ImageEvent[] = [{ action: 'Image uploaded', by: dto.uploadedBy ?? '', at: toLocalDateTime(dto.uploadDate) }];
  if (dto.updatedAt) events.push({ action: 'Details edited', by: dto.updatedBy ?? '', at: toLocalDateTime(dto.updatedAt) });
  return events;
}

export function toApiImage(dto: AssetImageDto, id = nextImageId++): ApiImage {
  return {
    id,
    apiId: dto.id,
    assetRegistrationId: dto.assetRegistrationId,
    assetTag: dto.assetTag?.trim() || dto.assetRegistrationId,
    assetName: dto.assetName?.trim() || '',
    title: dto.imageName,
    description: dto.description ?? '',
    type: dto.imageType,
    tags: dto.tags ?? [],
    uploadedBy: dto.uploadedBy ?? '',
    uploadedAt: toLocalDateTime(dto.uploadDate),
    location: dto.location ?? '',
    fileName: dto.fileName,
    size: dto.fileSize ?? 0,
    width: dto.width ?? 0,
    height: dto.height ?? 0,
    remarks: dto.remarks ?? '',
    src: dto.fileUrl,
    history: baseHistory(dto),
  };
}

function toFileRequest(item: ImageUploadItem, url: string): AssetImageFileRequest {
  return {
    imageName: item.title.trim() || null,
    fileName: item.file.name,
    fileUrl: url,
    contentType: item.file.type || null,
    fileSize: item.file.size,
    // The API rejects a zero size, so send none when the browser could not read it
    width: item.width > 0 ? item.width : null,
    height: item.height > 0 ? item.height : null,
  };
}

@Injectable({ providedIn: 'root' })
export class AssetImagesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly lookup = inject(AssetLookupService);
  private readonly apiUrl = `${ASSET_API}asset-images`;

  /** GET /api/asset-images (every page), optionally for one asset. */
  list(assetRegistrationId?: string): Observable<ApiImage[]> {
    return this.lookup
      .fetchAll<AssetImageDto>(this.apiUrl, assetRegistrationId ? { assetRegistrationId } : {})
      .pipe(map((items) => items.map((d) => toApiImage(d))));
  }

  /** Uploads each file (POST /api/media/upload, category "asset-images"), then POST /api/asset-images. */
  create(assetRegistrationIds: string[], items: ImageUploadItem[], value: ImageUploadValue): Observable<ApiImage[]> {
    const uploads = items.map((item) => this.lookup.uploadFile(item.file, 'asset-images').pipe(map((url) => toFileRequest(item, url))));
    return forkJoin(uploads).pipe(
      switchMap((images) => {
        const body: CreateAssetImagesRequest = {
          assetRegistrationIds,
          images,
          imageType: value.type,
          tags: value.tags,
          location: value.location.trim() || null,
          remarks: value.remarks.trim() || null,
          description: value.description.trim() || null,
          createdBy: this.auth.getUserEmail(),
          clientId: TENANT.clientId,
          tenantId: TENANT.tenantId,
        };
        return this.http.post<AssetImageDto[]>(this.apiUrl, body);
      }),
      map((created) => created.map((d) => toApiImage(d))),
    );
  }

  /** PUT /api/asset-images/{id}. Pass a file to replace the stored image (uploaded first). Keeps the local id. */
  update(image: ApiImage, value: ImageEditValue, replacement: ImageUploadItem | null = null): Observable<ApiImage> {
    const file$: Observable<AssetImageFileRequest | null> = replacement
      ? this.lookup.uploadFile(replacement.file, 'asset-images').pipe(map((url) => toFileRequest(replacement, url)))
      : of(null);
    return file$.pipe(
      switchMap((file) => {
        const body: UpdateAssetImageRequest = {
          assetRegistrationId: image.assetRegistrationId,
          imageName: value.title.trim(),
          imageType: value.type,
          tags: value.tags,
          location: value.location.trim() || null,
          remarks: value.remarks.trim() || null,
          description: value.description.trim() || null,
          file,
        };
        return this.http.put<AssetImageDto>(`${this.apiUrl}/${image.apiId}`, body);
      }),
      map((d) => toApiImage(d, image.id)),
    );
  }

  /** DELETE /api/asset-images/{id} */
  delete(apiId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${apiId}`);
  }

  /** POST /api/asset-images/bulk-delete */
  deleteMany(apiIds: string[]): Observable<number> {
    return this.http
      .post<{ deletedCount: number }>(`${this.apiUrl}/bulk-delete`, { ids: apiIds })
      .pipe(map((res) => res?.deletedCount ?? 0));
  }

  /** The image's history from GET /api/eventlogs?entityType=AssetImage&entityId={id}, oldest first. */
  history(image: ApiImage): Observable<ImageEvent[]> {
    return this.lookup.history('AssetImage', image.apiId).pipe(
      map((logs: EventLogDto[]) =>
        logs
          .map((log) => ({ action: ACTION_LABELS[log.action] ?? log.action, by: log.performedBy ?? '', at: toLocalDateTime(log.createdAt) }))
          .sort((a, b) => a.at.localeCompare(b.at)),
      ),
    );
  }
}
