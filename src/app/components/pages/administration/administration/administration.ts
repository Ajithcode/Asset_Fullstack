import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ADMINISTRATION_BASE_PATH, ADMINISTRATION_MENU } from '../administration-menu';

@Component({
  standalone: true,
  selector: 'app-administration',
  imports: [RouterLink],
  templateUrl: './administration.html',
  styleUrls: ['./administration.css']
})
export class Administration {
  readonly basePath = ADMINISTRATION_BASE_PATH;
  readonly modules = ADMINISTRATION_MENU;
}
