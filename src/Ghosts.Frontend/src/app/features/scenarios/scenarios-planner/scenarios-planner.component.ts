import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioService, ScenarioHubService, ObjectiveService, N8nWorkflowService } from '../../../core/services';
import { CreateScenario, ScenarioTimelineEvent, Objective, N8nWorkflow, ScenarioExtras } from '../../../core/models';
import { BuilderEntitiesComponent } from '../../scenario-builder/builder-entities/builder-entities.component';
import { BuilderGraphComponent } from '../../scenario-builder/builder-graph/builder-graph.component';

@Component({
  selector: 'app-scenarios-planner',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    DragDropModule,
    BuilderEntitiesComponent,
    BuilderGraphComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenarios-planner.component.html',
  styleUrls: ['./scenarios-planner.component.scss']
})
export class ScenariosPlannerComponent implements OnInit, OnDestroy {
  private readonly scenarioService = inject(ScenarioService);
  private readonly scenarioHub = inject(ScenarioHubService);
  private readonly objectiveService = inject(ObjectiveService);
  private readonly n8nWorkflowService = inject(N8nWorkflowService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected scenarioId: number | null = null;
  private loadedScenarioId: number | null = null;
  protected isEditMode = false;
  protected loading = signal(false);
  protected builderStatus = signal<string>('None');
  protected selectedTabIndex = 0;

  private readonly tabSlugs = ['parameters', 'technical-environment', 'simulation-mechanics', 'timeline', 'graph'];

  protected scenario: CreateScenario = {
    name: '',
    description: '',
    scenarioParameters: {
      nations: [],
      threatActors: [],
      injects: [],
      userPools: [],
      objectives: '',
      politicalContext: '',
      rulesOfEngagement: '',
      victoryConditions: ''
    },
    technicalEnvironment: {
      networkTopology: '',
      services: '',
      assets: '',
      defenses: [],
      vulnerabilities: [],
      platforms: {
        websites: [],
        socialMedia: [],
        emailProviders: [],
        cloudServices: [],
        collaborationTools: []
      }
    },
    simulationMechanics: {
      timelineType: 'real-time',
      durationHours: 8,
      adjudicationType: 'manual',
      escalationLadder: '',
      branchingLogic: '',
      telemetry: {
        collectLogs: true,
        collectNetwork: true,
        collectEndpoint: true,
        collectChat: true
      },
      performanceMetrics: ''
    },
    timeline: {
      exerciseDuration: 8,
      events: [
        {
          time: '00:00',
          number: 1,
          assigned: 'White Cell',
          description: 'STARTEX - Exercise begins',
          status: 'Pending',
          extras: { effects: {} }
        }
      ]
    },
    extras: this.withExtrasDefaults(null)
  };

  protected readonly cellRoles = ['None', 'White Cell', 'Red Team', 'Blue Team', 'Green Cell'];
  // An inject's owner is kept as the document writes it
  protected readonly documentOwners = [
    { value: 'white-cell', label: 'White Cell' },
    { value: 'red-team', label: 'Red Team' },
    { value: 'blue-team', label: 'Blue Team' },
    { value: 'green-cell', label: 'Green Cell' },
    { value: 'unassigned', label: 'None' }
  ];
  protected readonly triggerKinds = ['PointInTime', 'Scheduled', 'Triggered'];
  protected readonly moveDomains = ['cyber', 'cognitive', 'physical', 'hybrid'];
  protected readonly fogLevels = ['partial', 'full', 'off'];
  protected readonly sourceTypes = ['document', 'text', 'url'];
  protected readonly eventStatuses = ['Pending', 'Active', 'Complete'];
  protected readonly executionTypes: {value: string; label: string}[] = [
    { value: 'manual', label: 'Manual' },
    { value: 'workflow', label: 'Workflow' }
  ];
  protected readonly timelineColumns = ['drag', 'time', 'number', 'assigned', 'description', 'executionType', 'objective', 'status', 'actions'];
  protected scenarioObjectives = signal<Objective[]>([]);
  protected availableWorkflows = signal<N8nWorkflow[]>([]);

  protected readonly availablePlatforms = {
    websites: [
      { name: 'CNN', value: 'cnn.com' },
      { name: 'Wall Street Journal', value: 'wsj.com' },
      { name: 'New York Times', value: 'nytimes.com' },
      { name: 'BBC News', value: 'bbc.com' },
      { name: 'Reuters', value: 'reuters.com' }
    ],
    socialMedia: [
      { name: 'Facebook', value: 'facebook.com' },
      { name: 'X (Twitter)', value: 'x.com' },
      { name: 'Reddit', value: 'reddit.com' },
      { name: 'LinkedIn', value: 'linkedin.com' },
      { name: 'Discord', value: 'discord.com' },
      { name: 'Instagram', value: 'instagram.com' }
    ],
    emailProviders: [
      { name: 'Gmail', value: 'gmail.com' },
      { name: 'Outlook', value: 'outlook.com' },
      { name: 'Yahoo Mail', value: 'yahoo.com' },
      { name: 'ProtonMail', value: 'protonmail.com' }
    ],
    cloudServices: [
      { name: 'AWS', value: 'aws.amazon.com' },
      { name: 'Azure', value: 'azure.microsoft.com' },
      { name: 'Google Cloud', value: 'cloud.google.com' },
      { name: 'Dropbox', value: 'dropbox.com' },
      { name: 'Box', value: 'box.com' }
    ],
    collaborationTools: [
      { name: 'Slack', value: 'slack.com' },
      { name: 'Microsoft Teams', value: 'teams.microsoft.com' },
      { name: 'Zoom', value: 'zoom.us' },
      { name: 'Google Meet', value: 'meet.google.com' },
      { name: 'Webex', value: 'webex.com' }
    ]
  };

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const id = params['id'];
      if (id && id !== 'new') {
        this.scenarioId = +id;
        this.isEditMode = true;
        this.scenarioHub.connect(this.scenarioId);
        // Only (re)load when the scenario itself changes. A tab-only navigation
        // re-fires this subscription, and reloading here would overwrite unsaved edits.
        if (this.loadedScenarioId !== this.scenarioId) {
          this.loadedScenarioId = this.scenarioId;
          this.loadScenario(this.scenarioId);
        }
      } else if (this.loadedScenarioId === null) {
        this.loadedScenarioId = -1;
        this.loadAllObjectives();
      }

      const tab = params['tab'];
      if (tab) {
        const idx = this.tabSlugs.indexOf(tab);
        if (idx >= 0) this.selectedTabIndex = idx;
      }
    });
    this.loadWorkflows();
  }

  ngOnDestroy(): void {
    this.scenarioHub.disconnect();
  }

  private loadWorkflows(): void {
    this.n8nWorkflowService.getActiveWorkflows().subscribe({
      next: (workflows) => this.availableWorkflows.set(workflows),
      error: () => this.availableWorkflows.set([])
    });
  }

  protected onTabChanged(index: number): void {
    // Persist edits from the current tab before navigating away from it.
    this.autoSaveScenario();
    this.selectedTabIndex = index;
    const slug = this.tabSlugs[index] || this.tabSlugs[0];
    const id = this.scenarioId ?? 'new';
    this.router.navigate(['/scenarios', id, slug], { replaceUrl: true });
  }

  private loadAllObjectives(): void {
    this.objectiveService.getAll().subscribe({
      next: (objectives) => this.scenarioObjectives.set(this.flattenObjectives(objectives)),
      error: () => this.scenarioObjectives.set([])
    });
  }

  private flattenObjectives(objectives: Objective[]): Objective[] {
    const result: Objective[] = [];
    const walk = (list: Objective[], depth: number) => {
      for (const o of list) {
        result.push({ ...o, name: ' '.repeat(depth * 3) + o.name });
        if (o.children?.length) walk(o.children, depth + 1);
      }
    };
    walk(objectives, 0);
    return result;
  }

  protected loadScenario(id: number): void {
    this.loading.set(true);
    this.loadAllObjectives();
    this.scenarioService.getScenario(id).subscribe({
      next: (scenario) => {
        this.builderStatus.set(scenario.builderStatus ?? 'None');
        this.scenario = {
          name: scenario.name,
          description: scenario.description,
          scenarioParameters: scenario.scenarioParameters || this.scenario.scenarioParameters,
          technicalEnvironment: scenario.technicalEnvironment || this.scenario.technicalEnvironment,
          simulationMechanics: scenario.gameMechanics || scenario.simulationMechanics || this.scenario.simulationMechanics,
          timeline: scenario.timeline || this.scenario.timeline,
          extras: this.withExtrasDefaults(scenario.extras)
        };

        // Convert TTPs array to string for editing
        if (this.scenario.scenarioParameters) {
          this.scenario.scenarioParameters.threatActors = this.scenario.scenarioParameters.threatActors.map(actor => ({
            ...actor,
            ttpsString: actor.ttps.join(','),
            extras: { ...actor.extras, playbook: (actor.extras?.playbook ?? []).map(m => ({ ...m, effects: m.effects ?? {} })) }
          } as any));
          this.scenario.scenarioParameters.injects.forEach(i => i.extras = { ...i.extras, effects: i.extras?.effects ?? {} });
          this.scenario.scenarioParameters.userPools.forEach(p => p.extras = p.extras ?? {});
        }
        this.scenario.technicalEnvironment!.vulnerabilities.forEach(v => {
          v.extras = v.extras ?? {};
          // The cve column holds the description when there is no CVE: show it as the description
          if (v.cve && !/^CVE-\d{4}-\d{4,}$/.test(v.cve) && !v.extras.description) {
            v.extras.description = v.cve;
            v.cve = '';
          }
        });

        // Defenses are named in their column and described in extras: edit them as one list
        const details = this.terrain.defenses;
        this.terrain.defenses = this.scenario.technicalEnvironment!.defenses.map(name => details.find(d => d.name === name) ?? { name });

        // Normalize timeline event fields for mat-select binding
        if (this.scenario.timeline?.events) {
          this.scenario.timeline.events = this.scenario.timeline.events.map(e => ({
            ...e,
            objectiveIds: e.objectiveIds ?? [],
            executionType: (e.executionType || 'manual').toLowerCase() as any,
            workflowId: e.workflowId ?? undefined,
            extras: { ...e.extras, effects: e.extras?.effects ?? {} }
          }));
        }

        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading scenario', error);
        alert('Error loading scenario. Returning to list.');
        this.router.navigate(['/scenarios']);
        this.loading.set(false);
      }
    });
  }

  protected backToList(): void {
    this.router.navigate(['/scenarios']);
  }

  protected dismissBuilderPrompts(): void {
    this.builderStatus.set('Reviewed');
    if (this.scenarioId) {
      this.scenarioService.updateScenario(this.scenarioId, { ...this.scenario, gameMechanics: this.scenario.simulationMechanics, builderStatus: 'Reviewed' }).subscribe();
    }
  }

  protected openBuilder(): void {
    this.router.navigate(['/scenarios', this.scenarioId, 'builder']);
  }

  // Nation management
  protected addNation(): void {
    this.scenario.scenarioParameters!.nations.push({
      name: '',
      alignment: 'friendly'
    });
  }

  protected removeNation(index: number): void {
    this.scenario.scenarioParameters!.nations.splice(index, 1);
  }

  // Threat Actor management
  protected addThreatActor(): void {
    this.scenario.scenarioParameters!.threatActors.push({
      name: '',
      type: 'state',
      capability: 1,
      ttps: [],
      ttpsString: '', // Helper property for input binding
      extras: { playbook: [] }
    } as any);
  }

  protected removeThreatActor(index: number): void {
    this.scenario.scenarioParameters!.threatActors.splice(index, 1);
  }

  // Inject management
  protected addInject(): void {
    this.scenario.scenarioParameters!.injects.push({
      trigger: '',
      title: '',
      extras: { effects: {} }
    });
  }

  protected removeInject(index: number): void {
    this.scenario.scenarioParameters!.injects.splice(index, 1);
  }

  // User Pool management
  protected addUserPool(): void {
    this.scenario.scenarioParameters!.userPools.push({
      role: '',
      count: 1,
      extras: {}
    });
  }

  protected removeUserPool(index: number): void {
    this.scenario.scenarioParameters!.userPools.splice(index, 1);
  }

  // Timeline event management
  protected addTimelineEvent(): void {
    const newEvent: ScenarioTimelineEvent = {
      time: '00:00',
      number: this.scenario.timeline!.events.length + 1,
      assigned: 'White Cell',
      description: 'New event',
      status: 'Pending',
      objectiveIds: [],
      executionType: 'manual',
      extras: { effects: {} }
    };
    this.scenario.timeline!.events = [...this.scenario.timeline!.events, newEvent];
  }

  protected deleteTimelineEvent(index: number): void {
    this.scenario.timeline!.events = this.scenario.timeline!.events.filter((_, i) => i !== index);
    this.updateEventNumbers();
  }

  protected drop(event: CdkDragDrop<ScenarioTimelineEvent[]>): void {
    const events = [...this.scenario.timeline!.events];
    moveItemInArray(events, event.previousIndex, event.currentIndex);
    this.scenario.timeline!.events = events;
    this.updateEventNumbers();
  }

  protected updateEventNumbers(): void {
    this.scenario.timeline!.events = this.scenario.timeline!.events.map((event, index) => ({
      ...event,
      number: index + 1
    }));
  }

  // Platform management
  protected isPlatformSelected(category: keyof typeof this.availablePlatforms, value: string): boolean {
    const platforms = this.scenario.technicalEnvironment?.platforms;
    if (!platforms) return false;
    return (platforms[category] || []).includes(value);
  }

  protected togglePlatform(category: keyof typeof this.availablePlatforms, value: string): void {
    if (!this.scenario.technicalEnvironment) {
      return;
    }

    if (!this.scenario.technicalEnvironment.platforms) {
      this.scenario.technicalEnvironment.platforms = {
        websites: [],
        socialMedia: [],
        emailProviders: [],
        cloudServices: [],
        collaborationTools: []
      };
    }

    const platforms = this.scenario.technicalEnvironment.platforms?.[category];
    if (!platforms) return;

    const index = platforms.indexOf(value);

    if (index > -1) {
      platforms.splice(index, 1);
    } else {
      platforms.push(value);
    }
  }

  protected saveScenario(): void {
    // Convert ttpsString to ttps array for threat actors
    const scenarioToSave = { ...this.scenario };
    if (scenarioToSave.scenarioParameters) {
      scenarioToSave.scenarioParameters.threatActors = scenarioToSave.scenarioParameters.threatActors.map(actor => {
        const ttpsString = (actor as any).ttpsString || '';
        return {
          name: actor.name,
          type: actor.type,
          capability: actor.capability,
          ttps: ttpsString ? ttpsString.split(',').map((t: string) => t.trim()).filter((t: string) => t) : [],
          extras: actor.extras
        };
      });
    }
    this.syncDefenseNames();

    scenarioToSave.gameMechanics = scenarioToSave.simulationMechanics;

    if (this.isEditMode && this.scenarioId) {
      // Update existing scenario
      this.scenarioService.updateScenario(this.scenarioId, scenarioToSave).subscribe({
        next: () => {
          console.log('Scenario updated successfully');
          alert('Scenario updated successfully!');
          this.router.navigate(['/scenarios']);
        },
        error: (error) => {
          console.error('Error updating scenario', error);
          alert('Error updating scenario. Please check console for details.');
        }
      });
    } else {
      // Create new scenario
      this.scenarioService.createScenario(scenarioToSave).subscribe({
        next: (savedScenario) => {
          console.log('Scenario created successfully', savedScenario);
          alert('Scenario created successfully!');
          this.router.navigate(['/scenarios']);
        },
        error: (error) => {
          console.error('Error creating scenario', error);
          alert('Error creating scenario. Please check console for details.');
        }
      });
    }
  }

  protected onExecutionTypeChanged(element: ScenarioTimelineEvent): void {
    if (element.executionType !== 'workflow') {
      element.workflowId = undefined;
    }
    this.autoSaveScenario();
  }

  protected onObjectivesChanged(): void {
    this.autoSaveScenario();
  }

  protected onTimelineFieldChanged(): void {
    this.autoSaveScenario();
  }

  private autoSaveScenario(): void {
    if (!this.isEditMode || !this.scenarioId) return;
    const scenarioToSave = { ...this.scenario };
    if (scenarioToSave.scenarioParameters) {
      scenarioToSave.scenarioParameters.threatActors = scenarioToSave.scenarioParameters.threatActors.map(actor => {
        const ttpsString = (actor as any).ttpsString || '';
        return {
          name: actor.name,
          type: actor.type,
          capability: actor.capability,
          ttps: ttpsString ? ttpsString.split(',').map((t: string) => t.trim()).filter((t: string) => t) : [],
          extras: actor.extras
        };
      });
    }
    this.syncDefenseNames();
    scenarioToSave.gameMechanics = scenarioToSave.simulationMechanics;
    this.scenarioHub.updateScenario(this.scenarioId, scenarioToSave);
  }

  // What the scenario document says that no column holds (extras), with every nested part present
  // so the template can bind straight to it.
  protected get extras(): Required<ScenarioExtras> {
    return this.scenario.extras as Required<ScenarioExtras>;
  }

  protected get terrain(): Required<NonNullable<ScenarioExtras['terrain']>> {
    return this.extras.terrain as Required<NonNullable<ScenarioExtras['terrain']>>;
  }

  protected get rulesOfPlay(): Required<NonNullable<ScenarioExtras['rulesOfPlay']>> {
    return this.extras.rulesOfPlay as Required<NonNullable<ScenarioExtras['rulesOfPlay']>>;
  }

  private withExtrasDefaults(extras: ScenarioExtras | null | undefined): ScenarioExtras {
    const e = extras ?? {};
    return {
      ...e,
      catalog: e.catalog ?? {},
      context: e.context ?? {},
      audience: e.audience ?? {},
      terrain: {
        ...e.terrain,
        reference: e.terrain?.reference ?? {},
        segments: e.terrain?.segments ?? [],
        hosts: e.terrain?.hosts ?? [],
        services: e.terrain?.services ?? [],
        informationEnvironment: e.terrain?.informationEnvironment ?? {},
        defenses: e.terrain?.defenses ?? []
      },
      startingConditions: e.startingConditions ?? {},
      rulesOfPlay: {
        ...e.rulesOfPlay,
        clock: e.rulesOfPlay?.clock ?? {},
        deadline: e.rulesOfPlay?.deadline ?? {},
        escalationLadder: { rungs: e.rulesOfPlay?.escalationLadder?.rungs ?? [] }
      },
      sources: e.sources ?? [],
      references: e.references ?? []
    };
  }

  /** The defense column is the edited list's names; their details stay in extras. */
  private syncDefenseNames(): void {
    this.scenario.technicalEnvironment!.defenses = this.terrain.defenses.map(d => d.name).filter(n => n);
  }

  // Lists and maps are edited as text and parsed on change, so the input is not rewritten while typing
  protected commaText(values: string[] | undefined): string {
    return (values ?? []).join(', ');
  }

  protected setCommaList(target: object, key: string, text: string): void {
    (target as any)[key] = text.split(',').map(t => t.trim()).filter(t => t);
  }

  protected linesText(values: string[] | undefined): string {
    return (values ?? []).join('\n');
  }

  protected setLineList(target: object, key: string, text: string): void {
    (target as any)[key] = text.split('\n').map(t => t.trim()).filter(t => t);
  }

  protected factsText(facts: Record<string, string> | undefined): string {
    return Object.entries(facts ?? {}).map(([k, v]) => `${k}=${v}`).join('\n');
  }

  protected setFacts(target: object, key: string, text: string): void {
    const facts: Record<string, string> = {};
    for (const line of text.split('\n')) {
      const at = line.indexOf('=');
      if (at > 0) facts[line.slice(0, at).trim()] = line.slice(at + 1).trim();
    }
    (target as any)[key] = facts;
  }

  protected addItem<T>(list: T[], item: T): void {
    list.push(item);
  }

  protected removeItem<T>(list: T[], index: number): void {
    list.splice(index, 1);
  }

  protected exportScenario(): void {
    const dataStr = JSON.stringify(this.scenario, null, 2);
    const dataUri = 'data:application/json;charset=utf-8,' + encodeURIComponent(dataStr);
    const exportFileDefaultName = 'cyber-exercise-scenario.json';

    const linkElement = document.createElement('a');
    linkElement.setAttribute('href', dataUri);
    linkElement.setAttribute('download', exportFileDefaultName);
    linkElement.click();
  }
}
