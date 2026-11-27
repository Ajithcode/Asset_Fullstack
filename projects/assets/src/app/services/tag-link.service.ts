import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { environment } from 'shared-ui';

/** A tag from Tracking & IoT → Tag Management (GET /api/tag-ids), with the asset it is linked to. */
export interface LinkedTag {
  tagId: string;
  status: string;
  /** Asset ID of the linked asset (e.g. AR000012); null when the tag is not linked. */
  assetRegistrationId: string | null;
  /** The linked asset's code / tag and name. */
  assetCode: string | null;
  assetName: string | null;
}

interface TagIdPage {
  items: LinkedTag[];
  total: number;
  page: number;
  pageSize: number;
}

const PAGE_SIZE = 100;

/**
 * Which tag (if any) is linked to an asset, from the Tracking service's tag registry. Used before opening
 * Real-Time Location for an asset, so an asset without a tag gets a message instead of an empty map.
 */
@Injectable({ providedIn: 'root' })
export class TagLinkService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.trackingApiUrl}api/tag-ids`;

  /** Every tag that is linked to an asset (all pages). */
  linkedTags(): Observable<LinkedTag[]> {
    const page = (n: number) =>
      this.http
        .get<TagIdPage | LinkedTag[]>(this.url, {
          params: new HttpParams()
            .set('clientId', environment.clientId)
            .set('tenantId', environment.tenantId)
            .set('page', n)
            .set('pageSize', PAGE_SIZE),
        })
        .pipe(map((res) => (Array.isArray(res) ? { items: res, total: res.length, page: n, pageSize: res.length || PAGE_SIZE } : res)));
    return page(1).pipe(
      expand((res) => (res.page * res.pageSize < res.total && res.items.length ? page(res.page + 1) : EMPTY)),
      map((res) => res.items ?? []),
      reduce((all, items) => all.concat(items), [] as LinkedTag[]),
      map((all) => all.filter((t) => !!t.assetRegistrationId && (t.status ?? '').toLowerCase() !== 'inactive')),
    );
  }

  /** The tag linked to an asset, matched by its asset tag / code, Asset ID or (last) its name. */
  static findFor(tags: LinkedTag[], asset: { tag: string; name: string }): LinkedTag | null {
    const tag = asset.tag.trim().toUpperCase();
    const name = asset.name.trim().toLowerCase();
    return (
      (tag && tags.find((t) => (t.assetCode ?? '').trim().toUpperCase() === tag || (t.assetRegistrationId ?? '').toUpperCase() === tag)) ||
      (name && tags.find((t) => (t.assetName ?? '').trim().toLowerCase() === name)) ||
      null
    );
  }
}
