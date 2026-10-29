import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's CountryDto. */
export interface AppCountry {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryName: string;
  description: string;
  timeZone: string;
  countryCode: string;
  latitude: string;
  longitude: string;
  status: boolean;
}

/** Fields collected from the add/edit country form. */
export interface CountryFormValue {
  projectId: string;
  countryName: string;
  description: string;
  timeZone: string;
  countryCode: string;
  latitude: string;
  longitude: string;
  status: boolean;
}

/** Request body for POST {projectApiUrl}api/countries */
interface CreateCountryRequest extends CountryFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/countries/{id} — projectId cannot be changed via update. */
interface UpdateCountryRequest extends Omit<CountryFormValue, 'projectId'> {
  clientId: string;
  updatedBy: string;
}

/** Raw shape returned by the countries API. */
interface CountryDto {
  id: string;
  projectId: string;
  countryName: string;
  description: string;
  timeZone: string;
  countryCode: string;
  latitude: string;
  longitude: string;
  status: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string;
  tenantId: string;
  isDeleted: boolean;
}

function toAppCountry(dto: CountryDto): AppCountry {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryName: dto.countryName,
    description: dto.description,
    timeZone: dto.timeZone,
    countryCode: dto.countryCode,
    latitude: dto.latitude,
    longitude: dto.longitude,
    status: dto.status
  };
}

@Injectable({ providedIn: 'root' })
export class CountryService {
  private apiUrl = `${environment.projectApiUrl}api/countries`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/countries */
  getAll(): Observable<AppCountry[]> {
    return this.http.get<CountryDto[]>(this.apiUrl).pipe(map(list => list.map(toAppCountry)));
  }

  /** GET /api/countries/{id} */
  getById(id: string): Observable<AppCountry> {
    return this.http.get<CountryDto>(`${this.apiUrl}/${id}`).pipe(map(toAppCountry));
  }

  /** POST /api/countries */
  create(fields: CountryFormValue): Observable<AppCountry> {
    const request: CreateCountryRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId
    };
    return this.http.post<CountryDto>(this.apiUrl, request).pipe(map(toAppCountry));
  }

  /** PUT /api/countries/{id} (projectId is fixed at creation and cannot be changed here) */
  update(id: string, fields: CountryFormValue): Observable<AppCountry> {
    const { projectId, ...rest } = fields;
    const request: UpdateCountryRequest = {
      ...rest,
      clientId: environment.clientId,
      updatedBy: this.auth.getUserEmail() || 'unknown'
    };
    return this.http.put<CountryDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppCountry));
  }

  /** DELETE /api/countries/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
