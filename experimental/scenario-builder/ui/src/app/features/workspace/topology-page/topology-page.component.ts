import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceStore } from '../workspace.store';

/**
 * Where the range a scenario needs is built and deployed, for a kind that has a topology.
 * A stub for now: GraySpace and Updraft open read-only screens of their choices, and the others aren't built yet.
 */
@Component({
  selector: 'app-topology-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './topology-page.component.html',
  styleUrls: ['./topology-page.component.scss']
})
export class TopologyPageComponent {
  protected readonly store = inject(WorkspaceStore);

  /** A target with a screen opens it; the rest aren't built yet. */
  protected readonly targets: { id: string; name: string; description: string; open?: string }[] = [
    { id: 'grayspace', name: 'GraySpace', description: 'Builds the range through GraySpace’s Terrain API.', open: 'grayspace' },
    { id: 'updraft', name: 'Updraft', description: 'Builds the lab through the Updraft CLI: Gitea, Terraform and Packer on AWS.', open: 'updraft' },
    { id: 'crucible', name: 'Crucible', description: 'Builds the range through Crucible’s APIs.' },
    { id: 'other', name: 'Other Range', description: 'Builds the range through another range’s API.' }
  ];
}
