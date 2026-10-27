import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MASTER_DATA_MENU } from 'shared-ui';

/** Landing page: one card per sub-module. */
@Component({
  selector: 'app-master-data-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class MasterDataHome {
  readonly groups = MASTER_DATA_MENU;
}
