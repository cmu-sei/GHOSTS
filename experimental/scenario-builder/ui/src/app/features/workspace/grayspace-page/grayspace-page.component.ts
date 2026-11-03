import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AnswerValue } from '../../../core/models/builder.model';
import { labelFor } from '../../../shared/labels/question-label';
import { WorkspaceStore } from '../workspace.store';
import { GRAYSPACE_SAMPLE } from './grayspace-sample';

/** One GraySpace decision: what it settles, the Terrain API routes behind it, and the network answers that feed it. */
interface Decision {
  id: string;
  title: string;
  decides: string;
  api: string[];
  answers: string[];
}

/**
 * Deploying a scenario's range to GraySpace, laid out decision by decision. Read-only for now: it isn't
 * connected, so it shows GraySpace's sample choices beside the scenario's answers, and nothing is saved or sent.
 */
@Component({
  selector: 'app-grayspace-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './grayspace-page.component.html',
  styleUrls: ['./grayspace-page.component.scss']
})
export class GraySpacePageComponent {
  protected readonly store = inject(WorkspaceStore);
  protected readonly sample = GRAYSPACE_SAMPLE;

  protected readonly decisions: Decision[] = [
    { id: 'connection', title: 'Connection', decides: 'Which GraySpace builds the range, and whether it can run jobs now.', api: ['GET /capabilities'], answers: [] },
    {
      id: 'base', title: 'Base', decides: 'The simulated internet the enclave joins: its version and health. Managing it is GraySpace admin work.',
      api: ['GET /base', 'GET /base/versions', 'GET /base/inventory', 'GET /base/dns', 'GET /base/preflight', 'POST /base/deploy', 'POST /base/configure', 'DELETE /base'], answers: []
    },
    {
      id: 'enclave', title: 'Enclave', decides: 'Build a new enclave from this scenario, or reuse one already on GraySpace. Each save is a new version.',
      api: ['GET /slices', 'GET /slices/{slug}', 'PUT /slices/{slug}', 'DELETE /slices/{slug}'], answers: ['rangeReference', 'buildLimits']
    },
    {
      id: 'sites', title: 'Sites and Segments', decides: 'Where each site’s edge router joins the base, how it’s addressed, and the segments behind it.',
      api: ['GET /attachment-points'], answers: ['segments', 'connectivity']
    },
    {
      id: 'hosts', title: 'Hosts', decides: 'Each host’s segment, image, roles, size and count, and whether it gets a public address.',
      api: ['GET /images', 'POST /images', 'DELETE /images/{name}'], answers: ['hosts', 'services', 'platforms', 'monitoring', 'telemetry', 'simulatedUsers', 'planted']
    },
    { id: 'roles', title: 'Role Settings', decides: 'The values a role needs to be set up, given once for the enclave.', api: ['GET /roles'], answers: ['identity'] },
    {
      id: 'dns', title: 'DNS and Certificates', decides: 'The enclave’s domains and records, and the certificates its hosts serve.',
      api: ['GET /capabilities', 'GET /ca/bundle'], answers: []
    },
    { id: 'conditions', title: 'Network Conditions', decides: 'Latency, jitter, loss and bandwidth on the base’s links while the instance runs.', api: ['GET /map'], answers: [] },
    {
      id: 'deploy', title: 'Deploy', decides: 'Check the enclave, plan it, run it as an instance, follow the job, act on it, and take it down.',
      api: ['POST /slices/{slug}/validate', 'POST /slices/{slug}/plan', 'POST /instances', 'GET /instances/{id}', 'GET /jobs/{id}/log', 'POST /jobs/{id}/cancel',
        'POST /jobs/{id}/retry', 'POST /instances/{id}/actions', 'DELETE /instances/{id}', 'GET /events'], answers: []
    }
  ];

  /** The enclave's name GraySpace would get: the scenario's name, lowercased and hyphenated. */
  protected readonly slug = computed(() => (this.store.name() ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''));

  /** The answers that feed a decision, for the questions this scenario type asks. */
  protected answersFor(decision: Decision): { label: string; value?: AnswerValue }[] {
    const scenarioType = this.store.scenarioType();
    const asked = new Set(scenarioType?.stages.flatMap(s => s.questions.map(q => q.id)));
    return decision.answers.filter(id => asked.has(id)).map(id => ({ label: labelFor(id, scenarioType), value: this.store.answer(id)?.value }));
  }

  protected isList(value: AnswerValue): value is string[] {
    return Array.isArray(value);
  }
}
