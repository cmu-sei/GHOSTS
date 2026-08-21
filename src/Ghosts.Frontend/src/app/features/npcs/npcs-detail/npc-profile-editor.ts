import { ChangeDetectionStrategy, Component, EventEmitter, inject, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NpcNameId, NpcProfile } from '../../../core/models';

export const MENTAL_HEALTH_KEYS = [
  'interpersonalSkills', 'adherenceToPolicy', 'enthusiasmAndAttitude', 'openToFeedback',
  'generalPerformance', 'overallPerformance', 'iq', 'spideySense',
  'senseSomethingIsWrongQuotient', 'happyQuotient', 'melancholyQuotient'
];

export const MOTIVATION_KEYS = [
  'acceptance', 'beauty', 'curiosity', 'eating', 'family', 'honor', 'idealism', 'independence',
  'order', 'physicalActivity', 'power', 'saving', 'socialContact', 'status', 'tranquility', 'vengeance'
];

/** camelCase property name to something readable in a label */
export function labelForKey(key: string): string {
  const spaced = key.replace(/([A-Z])/g, ' $1');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

@Component({
  selector: 'app-npc-profile-editor',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    MatTooltipModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './npc-profile-editor.html',
  styleUrl: './npc-profile-editor.scss'
})
export class NpcProfileEditor implements OnInit {
  /** The profile being edited. Anything not on the form is carried through untouched. */
  @Input({ required: true }) profile!: NpcProfile;
  /** Every NPC in the system, so relationships can be pointed at one of them */
  @Input() npcOptions: NpcNameId[] = [];
  @Input() saving = false;

  @Output() readonly save = new EventEmitter<NpcProfile>();
  @Output() readonly cancelled = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);

  protected form!: FormGroup;

  protected readonly mentalHealthKeys = MENTAL_HEALTH_KEYS;
  protected readonly motivationKeys = MOTIVATION_KEYS;
  protected readonly labelForKey = labelForKey;

  protected readonly biologicalSexes = ['Male', 'Female'];
  protected readonly degreeLevels = [
    'None', 'GED', 'HSDiploma', 'Associates', 'Bachelors', 'Masters', 'Doctorate', 'Professional'
  ];
  protected readonly employmentStatuses = [
    'FullTime', 'PartTime', 'Suspended', 'Temporary', 'Resigned', 'Terminated'
  ];
  protected readonly relationshipTypes = [
    'Coworker', 'Teammate', 'Supervisor', 'Direct Report', 'Friend', 'Neighbor', 'Acquaintance'
  ];

  ngOnInit(): void {
    const p = this.profile ?? {};
    this.form = this.fb.group({
      name: this.fb.group({
        prefix: [p.name?.prefix ?? ''],
        first: [p.name?.first ?? ''],
        middle: [p.name?.middle ?? ''],
        last: [p.name?.last ?? ''],
        suffix: [p.name?.suffix ?? '']
      }),
      email: [p.email ?? ''],
      password: [p.password ?? ''],
      homePhone: [p.homePhone ?? ''],
      cellPhone: [p.cellPhone ?? ''],
      cac: [p.cac ?? ''],
      biologicalSex: [p.biologicalSex ?? 'Male'],
      birthdate: [this.dateIn(p.birthdate)],
      address: this.fb.array((p.address ?? []).map(a => this.addressGroup(a))),
      workstation: this.fb.group({
        name: [p.workstation?.name ?? ''],
        domain: [p.workstation?.domain ?? ''],
        username: [p.workstation?.username ?? ''],
        password: [p.workstation?.password ?? ''],
        ipAddress: [p.workstation?.ipAddress ?? '']
      }),
      unitCountry: [p.unit?.country ?? ''],
      rank: this.fb.group({
        branch: [p.rank?.branch ?? ''],
        classification: [p.rank?.classification ?? ''],
        name: [p.rank?.name ?? ''],
        abbr: [p.rank?.abbr ?? ''],
        pay: [p.rank?.pay ?? ''],
        billet: [p.rank?.billet ?? ''],
        mos: [p.rank?.mos ?? ''],
        mosid: [p.rank?.mosid ?? '']
      }),
      accounts: this.fb.array((p.accounts ?? []).map(a => this.fb.group({
        url: [a.url ?? ''],
        username: [a.username ?? ''],
        password: [a.password ?? '']
      }))),
      preferences: this.fb.array((p.preferences ?? []).map(x => this.fb.group({
        name: [x.name ?? ''],
        score: [x.score ?? 0],
        meta: [x.meta ?? '']
      }))),
      education: this.fb.group({
        degrees: this.fb.array((p.education?.degrees ?? []).map(d => this.fb.group({
          level: [d.level ?? 'HSDiploma'],
          degreeType: [d.degreeType ?? ''],
          major: [d.major ?? ''],
          school: this.fb.group({
            name: [d.school?.name ?? ''],
            location: [d.school?.location ?? '']
          })
        })))
      }),
      employment: this.fb.group({
        employmentRecords: this.fb.array((p.employment?.employmentRecords ?? []).map(j => this.fb.group({
          jobTitle: [j.jobTitle ?? ''],
          company: [j.company ?? ''],
          department: [j.department ?? ''],
          organization: [j.organization ?? ''],
          startDate: [this.dateIn(j.startDate)],
          endDate: [this.dateIn(j.endDate)],
          employmentStatus: [j.employmentStatus ?? 'FullTime'],
          level: [j.level ?? 0],
          salary: [j.salary ?? 0],
          email: [j.email ?? ''],
          emailSuffix: [j.emailSuffix ?? ''],
          phone: [j.phone ?? ''],
          address: this.addressGroup(j.address ?? {})
        })))
      }),
      health: this.fb.group({
        height: [p.health?.height ?? 0],
        weight: [p.health?.weight ?? 0],
        bloodType: [p.health?.bloodType ?? ''],
        preferredMeal: [p.health?.preferredMeal ?? ''],
        medicalConditions: this.fb.array((p.health?.medicalConditions ?? []).map(c => this.fb.group({
          name: [c.name ?? ''],
          prescriptions: this.fb.array((c.prescriptions ?? []).map(x => this.fb.group({
            name: [x.name ?? '']
          })))
        })))
      }),
      finances: this.fb.group({
        netWorth: [p.finances?.netWorth ?? 0],
        totalDebt: [p.finances?.totalDebt ?? 0],
        creditCards: this.fb.array((p.finances?.creditCards ?? []).map(c => this.fb.group({
          number: [c.number ?? ''],
          type: [c.type ?? '']
        })))
      }),
      foreignTravel: this.fb.group({
        trips: this.fb.array((p.foreignTravel?.trips ?? []).map(t => this.fb.group({
          destination: [t.destination ?? ''],
          country: [t.country ?? ''],
          code: [t.code ?? ''],
          arriveDestination: [this.dateIn(t.arriveDestination)],
          departDestination: [this.dateIn(t.departDestination)]
        })))
      }),
      family: this.fb.group({
        members: this.fb.array((p.family?.members ?? []).map(m => this.fb.group({
          relationship: [m.relationship ?? ''],
          name: this.fb.group({
            first: [m.name?.first ?? ''],
            middle: [m.name?.middle ?? ''],
            last: [m.name?.last ?? ''],
            suffix: [m.name?.suffix ?? '']
          })
        })))
      }),
      career: this.fb.group({
        workEthic: [p.career?.workEthic ?? 0],
        teamValue: [p.career?.teamValue ?? 0],
        strengths: this.fb.array((p.career?.strengths ?? []).map(x => this.fb.group({ name: [x.name ?? ''] }))),
        weaknesses: this.fb.array((p.career?.weaknesses ?? []).map(x => this.fb.group({ name: [x.name ?? ''] })))
      }),
      mentalHealth: this.numberGroup(MENTAL_HEALTH_KEYS, p.mentalHealth),
      motivationalProfile: this.numberGroup(MOTIVATION_KEYS, p.motivationalProfile),
      insiderThreat: this.fb.group({
        access: this.fb.group({
          securityClearance: [p.insiderThreat?.access?.securityClearance ?? ''],
          systemsAccess: [p.insiderThreat?.access?.systemsAccess ?? ''],
          physicalAccess: [p.insiderThreat?.access?.physicalAccess ?? ''],
          explosivesAccess: [p.insiderThreat?.access?.explosivesAccess ?? ''],
          cbrnAccess: [p.insiderThreat?.access?.cbrnAccess ?? ''],
          isDoDSystemsPrivilegedUser: [p.insiderThreat?.access?.isDoDSystemsPrivilegedUser ?? false]
        }),
        isBackgroundCheckStatusClear: [p.insiderThreat?.isBackgroundCheckStatusClear ?? false]
      }),
      attributes: this.fb.array(Object.entries(p.attributes ?? {}).map(([key, value]) => this.fb.group({
        key: [key],
        value: [value]
      }))),
      relationships: this.fb.array((p.relationships ?? []).map(r => this.fb.group({
        with: [r.with ?? ''],
        type: [r.type ?? '']
      })))
    });
  }

  protected array(path: string): FormArray {
    return this.form.get(path) as FormArray;
  }

  protected prescriptions(conditionIndex: number): FormArray {
    return this.array('health.medicalConditions').at(conditionIndex).get('prescriptions') as FormArray;
  }

  protected removeAt(path: string, index: number): void {
    this.array(path).removeAt(index);
  }

  protected addAddress(): void {
    this.array('address').push(this.addressGroup({}));
  }

  protected addAccount(): void {
    this.array('accounts').push(this.fb.group({ url: [''], username: [''], password: [''] }));
  }

  protected addPreference(): void {
    this.array('preferences').push(this.fb.group({ name: [''], score: [50], meta: [''] }));
  }

  protected addDegree(): void {
    this.array('education.degrees').push(this.fb.group({
      level: ['Bachelors'],
      degreeType: [''],
      major: [''],
      school: this.fb.group({ name: [''], location: [''] })
    }));
  }

  protected addJob(): void {
    this.array('employment.employmentRecords').push(this.fb.group({
      jobTitle: [''],
      company: [''],
      department: [''],
      organization: [''],
      startDate: [''],
      endDate: [''],
      employmentStatus: ['FullTime'],
      level: [0],
      salary: [0],
      email: [''],
      emailSuffix: [''],
      phone: [''],
      address: this.addressGroup({})
    }));
  }

  protected addMedicalCondition(): void {
    this.array('health.medicalConditions').push(this.fb.group({
      name: [''],
      prescriptions: this.fb.array([] as FormGroup[])
    }));
  }

  protected addPrescription(conditionIndex: number): void {
    this.prescriptions(conditionIndex).push(this.fb.group({ name: [''] }));
  }

  protected addCreditCard(): void {
    this.array('finances.creditCards').push(this.fb.group({ number: [''], type: [''] }));
  }

  protected addTrip(): void {
    this.array('foreignTravel.trips').push(this.fb.group({
      destination: [''],
      country: [''],
      code: [''],
      arriveDestination: [''],
      departDestination: ['']
    }));
  }

  protected addFamilyMember(): void {
    this.array('family.members').push(this.fb.group({
      relationship: [''],
      name: this.fb.group({ first: [''], middle: [''], last: [''], suffix: [''] })
    }));
  }

  protected addTrait(path: string): void {
    this.array(path).push(this.fb.group({ name: [''] }));
  }

  protected addAttribute(): void {
    this.array('attributes').push(this.fb.group({ key: [''], value: [''] }));
  }

  protected addRelationship(): void {
    this.array('relationships').push(this.fb.group({ with: [''], type: ['Coworker'] }));
  }

  protected onSubmit(): void {
    const v = this.form.getRawValue();

    this.save.emit({
      ...this.profile,
      name: v.name,
      email: v.email,
      password: v.password,
      homePhone: v.homePhone,
      cellPhone: v.cellPhone,
      cac: v.cac,
      biologicalSex: v.biologicalSex,
      birthdate: this.dateOut(v.birthdate) ?? this.profile.birthdate,
      address: v.address,
      workstation: v.workstation,
      // only the country is editable here, the unit tree itself is generated
      unit: { ...this.profile.unit, country: v.unitCountry },
      rank: { ...this.profile.rank, ...v.rank },
      accounts: v.accounts,
      preferences: v.preferences.map((x: any, i: number) => ({ ...x, id: i })),
      education: { degrees: v.education.degrees },
      employment: {
        employmentRecords: v.employment.employmentRecords.map((j: any) => ({
          ...j,
          startDate: this.dateOut(j.startDate) ?? this.epoch,
          endDate: this.dateOut(j.endDate)
        }))
      },
      health: v.health,
      finances: v.finances,
      foreignTravel: {
        trips: v.foreignTravel.trips.map((t: any) => ({
          ...t,
          arriveDestination: this.dateOut(t.arriveDestination) ?? this.epoch,
          departDestination: this.dateOut(t.departDestination) ?? this.epoch
        }))
      },
      family: v.family,
      career: v.career,
      mentalHealth: v.mentalHealth,
      motivationalProfile: v.motivationalProfile,
      insiderThreat: {
        ...this.profile.insiderThreat,
        access: { ...this.profile.insiderThreat?.access, ...v.insiderThreat.access },
        isBackgroundCheckStatusClear: v.insiderThreat.isBackgroundCheckStatusClear
      },
      attributes: Object.fromEntries(
        v.attributes.filter((a: any) => !!a.key).map((a: any) => [a.key, a.value ?? ''])
      ),
      relationships: v.relationships
        .filter((r: any) => !!r.with)
        .map((r: any, i: number) => ({ id: i, with: r.with, type: r.type }))
    });
  }

  private readonly epoch = '0001-01-01T00:00:00';

  private addressGroup(a: { addressType?: string; name?: string; address1?: string; address2?: string; city?: string; state?: string; postalCode?: string }): FormGroup {
    return this.fb.group({
      addressType: [a.addressType ?? ''],
      name: [a.name ?? ''],
      address1: [a.address1 ?? ''],
      address2: [a.address2 ?? ''],
      city: [a.city ?? ''],
      state: [a.state ?? ''],
      postalCode: [a.postalCode ?? '']
    });
  }

  private numberGroup(keys: string[], source: object | undefined): FormGroup {
    const values = (source ?? {}) as Record<string, number | undefined>;
    const controls: Record<string, unknown[]> = {};
    for (const key of keys) {
      controls[key] = [values[key] ?? 0];
    }
    return this.fb.group(controls);
  }

  /** iso date to the yyyy-MM-dd a date input needs */
  private dateIn(value?: string): string {
    return value ? value.substring(0, 10) : '';
  }

  private dateOut(value?: string): string | null {
    return value ? `${value}T00:00:00` : null;
  }
}
