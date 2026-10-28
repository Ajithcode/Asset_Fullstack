import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MASTER_DATA_MENU, MasterDataGroup } from 'shared-ui';

/** Sub-module landing page (e.g. Asset): one card per master in that group. */
@Component({
  selector: 'app-master-data-group-home',
  imports: [RouterLink],
  templateUrl: './group-home.html',
  styleUrl: './home.css'
})
export class GroupHome {
  readonly group: MasterDataGroup | undefined = MASTER_DATA_MENU.find(
    (g) => g.slug === inject(ActivatedRoute).snapshot.data['group']
  );
}
