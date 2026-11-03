import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AnswerValue } from '../../../core/models/builder.model';
import { labelFor } from '../../../shared/labels/question-label';
import { WorkspaceStore } from '../workspace.store';
import { UPDRAFT_SAMPLE } from './updraft-sample';

/** One Updraft decision: what it settles, the CLI commands behind it, and the network answers that feed it. */
interface Decision {
  id: string;
  title: string;
  decides: string;
  commands: string[];
  answers: string[];
}

/**
 * Deploying a scenario's range as an Updraft lab, laid out decision by decision. Read-only for now: it isn't
 * connected, so it shows what the Updraft CLI offers beside the scenario's answers, and nothing is saved or run.
 * Updraft has no network segments, so the segments and connectivity answers have no place here.
 */
@Component({
  selector: 'app-updraft-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './updraft-page.component.html',
  styleUrls: ['../grayspace-page/grayspace-page.component.scss']
})
export class UpdraftPageComponent {
  protected readonly store = inject(WorkspaceStore);
  protected readonly sample = UPDRAFT_SAMPLE;

  protected readonly decisions: Decision[] = [
    { id: 'connection', title: 'Connection', decides: 'Which Gitea and AWS account build the lab, from the settings the workspace template writes.', commands: ['updraft lab list'], answers: [] },
    {
      id: 'lab', title: 'Lab', decides: 'Make a lab repo for this scenario from Updraft’s lab template, or open one you already have.',
      commands: ['updraft lab list', 'updraft lab new', 'updraft lab open', 'updraft lab status'], answers: ['rangeReference']
    },
    {
      id: 'images', title: 'Images', decides: 'The AMIs hosts boot from: a public image, a library image, or one this lab builds with Packer and promotes to the library.',
      commands: ['updraft image list', 'updraft image new', 'updraft image build', 'updraft image promote'], answers: ['buildLimits']
    },
    {
      id: 'hosts', title: 'Hosts', decides: 'Each host’s image, instance type, number, console and username, or a host cloned from a published lab.',
      commands: ['updraft host list', 'updraft host add', 'updraft host rm', 'updraft host clone'],
      answers: ['hosts', 'identity', 'monitoring', 'telemetry', 'simulatedUsers', 'planted']
    },
    {
      id: 'services', title: 'Services and Zones', decides: 'The DNS names each host serves, whether over TLS, and the zones that cover them.',
      commands: ['updraft service add', 'updraft service rm'], answers: ['services', 'platforms']
    },
    { id: 'deploy', title: 'Deploy', decides: 'Apply the lab as a projection, check its hosts, and take it down.', commands: ['updraft deploy', 'updraft status', 'updraft down'], answers: [] },
    { id: 'players', title: 'Players', decides: 'Who can reach a projection, by email.', commands: ['updraft grant'], answers: [] },
    {
      id: 'publish', title: 'Publish', decides: 'Tag a version of the lab and add it to the library, so others can clone its hosts.',
      commands: ['updraft publish', 'updraft publish --status', 'updraft library labs'], answers: []
    }
  ];

  /** The lab's name: the scenario's name, lowercased and hyphenated. */
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
