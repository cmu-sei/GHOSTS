import { Component, OnInit, inject, signal } from '@angular/core';
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
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioService } from '../../../core/services';
import { CreateScenario, ScenarioTimelineEvent, Scenario } from '../../../core/models';

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
    DragDropModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenarios-planner.component.html',
  styleUrls: ['./scenarios-planner.component.scss']
})
export class ScenariosPlannerComponent implements OnInit {
  private readonly scenarioService = inject(ScenarioService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected scenarioId: number | null = null;
  protected isEditMode = false;
  protected loading = signal(false);

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
          status: 'Pending'
        }
      ]
    }
  };

  protected readonly cellRoles = ['White Cell', 'Red Team', 'Blue Team', 'Green Cell'];
  protected readonly eventStatuses = ['Pending', 'Active', 'Complete'];
  protected readonly timelineColumns = ['drag', 'time', 'number', 'assigned', 'description', 'status', 'actions'];

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
        this.loadScenario(this.scenarioId);
      }
    });
  }

  protected loadScenario(id: number): void {
    this.loading.set(true);
    this.scenarioService.getScenario(id).subscribe({
      next: (scenario) => {
        this.scenario = {
          name: scenario.name,
          description: scenario.description,
          scenarioParameters: scenario.scenarioParameters || this.scenario.scenarioParameters,
          technicalEnvironment: scenario.technicalEnvironment || this.scenario.technicalEnvironment,
          simulationMechanics: scenario.simulationMechanics || this.scenario.simulationMechanics,
          timeline: scenario.timeline || this.scenario.timeline
        };

        // Convert TTPs array to string for editing
        if (this.scenario.scenarioParameters) {
          this.scenario.scenarioParameters.threatActors = this.scenario.scenarioParameters.threatActors.map(actor => ({
            ...actor,
            ttpsString: actor.ttps.join(',')
          } as any));
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
      ttpsString: '' // Helper property for input binding
    } as any);
  }

  protected removeThreatActor(index: number): void {
    this.scenario.scenarioParameters!.threatActors.splice(index, 1);
  }

  // Inject management
  protected addInject(): void {
    this.scenario.scenarioParameters!.injects.push({
      trigger: '',
      title: ''
    });
  }

  protected removeInject(index: number): void {
    this.scenario.scenarioParameters!.injects.splice(index, 1);
  }

  // User Pool management
  protected addUserPool(): void {
    this.scenario.scenarioParameters!.userPools.push({
      role: '',
      count: 1
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
      status: 'Pending'
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
          ttps: ttpsString ? ttpsString.split(',').map((t: string) => t.trim()).filter((t: string) => t) : []
        };
      });
    }

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
