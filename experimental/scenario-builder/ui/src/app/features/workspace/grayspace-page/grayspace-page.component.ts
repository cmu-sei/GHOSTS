import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe, JsonPipe, NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Observable, Subscription, firstValueFrom } from 'rxjs';
import { AnswerValue } from '../../../core/models/builder.model';
import {
  BaseJobView, EnclaveAnswer, EnclaveView, FollowView, GraySpaceOffers, GraySpaceStatus, GsAttach, GsFinding, GsHost, GsJob, GsPreflight,
  GsPutSliceResult, GsRole, GsServer, GsValidateResult, SliceSpec
} from '../../../core/models/grayspace.model';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { labelFor } from '../../../shared/labels/question-label';
import { WorkspaceStore } from '../workspace.store';

/** One GraySpace decision: what it settles, the Terrain API routes behind it, and the network answers that feed it. */
interface Decision {
  id: string;
  title: string;
  decides: string;
  api: string[];
  answers: string[];
}

/** A place a site's edge or an internet host can join the base, as a select option. */
interface AttachOption {
  value: string;
  label: string;
  attach: GsAttach;
}

/**
 * Deploying a scenario's range to GraySpace, decision by decision: connect, see what GraySpace offers beside the scenario's
 * answers, build the enclave (saved as a draft as it's edited), then validate, save, plan, create, follow and destroy it.
 * The builder's API makes every GraySpace call; the token never reaches the browser.
 */
@Component({
  selector: 'app-grayspace-page',
  standalone: true,
  imports: [RouterLink, DatePipe, JsonPipe, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './grayspace-page.component.html',
  styleUrls: ['./grayspace-page.component.scss']
})
export class GraySpacePageComponent {
  protected readonly store = inject(WorkspaceStore);
  private readonly api = inject(BuilderApiService);
  private readonly me = inject(MeService);

  protected readonly decisions: Decision[] = [
    { id: 'connection', title: 'Connection', decides: 'Which GraySpace builds the range, and whether it can run jobs now.', api: ['GET /server'], answers: [] },
    {
      id: 'base', title: 'Base', decides: 'The simulated internet the enclave joins: its version and health. An admin deploys, configures or destroys it.',
      api: ['GET /base', 'GET /base/versions', 'GET /base/preflight', 'POST /base/deploy', 'POST /base/configure', 'DELETE /base', 'GET /jobs?target=base'], answers: []
    },
    {
      id: 'enclave', title: 'Enclave', decides: 'Build a new enclave from this scenario, or reuse one already on GraySpace. Each save is a new version.',
      api: ['GET /slices', 'GET /slices/{slug}'], answers: ['rangeReference', 'buildLimits']
    },
    {
      id: 'sites', title: 'Sites and Segments', decides: 'Where each site’s edge router joins the base, how it’s addressed, and the segments behind it.',
      api: ['GET /attachment-points'], answers: ['segments', 'connectivity']
    },
    {
      id: 'hosts', title: 'Hosts', decides: 'Each host’s segment, image, roles, size and count, and whether it gets a public address.',
      api: ['GET /server'], answers: ['hosts', 'services', 'platforms', 'monitoring', 'telemetry', 'simulatedUsers', 'planted']
    },
    { id: 'roles', title: 'Role Settings', decides: 'The values a role needs to be set up, given once for the enclave.', api: ['GET /server'], answers: ['identity'] },
    { id: 'dns', title: 'DNS and Certificates', decides: 'The enclave’s domains and records, and the certificates its hosts serve.', api: ['GET /capabilities'], answers: [] },
    { id: 'conditions', title: 'Network Conditions', decides: 'Latency, jitter, loss and bandwidth on the base’s links while the instance runs.', api: ['GET /map'], answers: [] },
    {
      id: 'deploy', title: 'Deploy', decides: 'Check the enclave, plan it, run it as an instance, follow the job, and take it down.',
      api: ['POST /slices/{slug}/validate', 'PUT /slices/{slug}', 'POST /slices/{slug}/plan', 'POST /instances', 'GET /instances/{id}', 'GET /jobs/{id}/log',
        'POST /jobs/{id}/cancel', 'POST /jobs/{id}/retry', 'DELETE /instances/{id}'], answers: []
    }
  ];

  protected readonly status = signal<GraySpaceStatus | null>(null);
  protected readonly offers = signal<GraySpaceOffers | null>(null);
  protected readonly enclave = signal<EnclaveView | null>(null);
  protected readonly follow = signal<FollowView | null>(null);
  protected readonly baseFollow = signal<BaseJobView | null>(null);
  /** The last check of the chosen version and scope; picking another clears it, so Deploy waits for a new check. */
  protected readonly preflight = signal<GsPreflight | null>(null);
  /** What the last base step said, or why it failed. */
  protected readonly baseNote = signal<string | null>(null);
  /** What Deploy everything is doing now, or where it stopped. */
  protected readonly quick = signal<string | null>(null);
  protected readonly quickRunning = signal(false);
  /** The last validate or save's findings, or a refusal's. */
  protected readonly findings = signal<GsFinding[] | null>(null);
  /** What the last deploy step said, or why it failed. */
  protected readonly note = signal<string | null>(null);
  protected readonly connectionError = signal<string | null>(null);
  protected readonly draftError = signal<string | null>(null);
  /** The step running now; every GraySpace button waits for it. */
  protected readonly busy = signal<string | null>(null);

  /** The address a connect is trying now; pressing Connect again drops that try for a new one. */
  protected readonly connectingTo = signal<string | null>(null);
  protected readonly draftUrl = signal('');
  protected readonly draftToken = signal('');
  protected readonly reuseSlug = signal('');
  protected readonly instancePrefix = signal('');
  protected readonly instanceVersion = signal('');
  protected readonly baseVersion = signal('');
  protected readonly baseScope = signal('');

  protected readonly admin = computed(() => this.me.me()?.admin ?? false);
  /** A GraySpace is saved, though it may not answer now. */
  protected readonly saved = computed(() => this.status()?.connected ?? false);
  /** The saved GraySpace answered just now; every GraySpace step waits for it. */
  protected readonly connected = computed(() => this.saved() && !!this.status()?.answering);
  protected readonly spec = computed(() => this.enclave()?.spec ?? null);
  protected readonly server = computed(() => this.offers()?.server ?? null);
  protected readonly capabilities = computed(() => this.offers()?.capabilities ?? null);
  protected readonly images = computed(() => this.server()?.images ?? []);
  protected readonly roles = computed(() => this.server()?.roles ?? []);
  protected readonly links = computed(() => (this.offers()?.map?.features ?? []).filter(f => f.properties.kind === 'link'));
  protected readonly segments = computed(() => (this.spec()?.sites ?? []).flatMap(site => site.segments.map(s => ({ site: site.name, name: s.name }))));
  protected readonly versions = computed(() => this.offers()?.slices?.find(s => s.slug === this.spec()?.slug)?.versions ?? []);
  protected readonly running = computed(() => !!this.follow()?.job && !this.follow()!.job!.finished);
  protected readonly baseRunning = computed(() => !!this.baseFollow()?.job && !this.baseFollow()!.job!.finished);
  protected readonly baseDeployed = computed(() => !!this.offers()?.base && this.offers()!.base!.status !== 'not_deployed');
  /** Whether a base job can start now: GraySpace can run them, and none is running. */
  protected readonly baseReady = computed(() => this.connected() && !this.busy() && !this.baseRunning() && !!this.server()?.base_jobs);
  /** The version a deploy uses: the one picked, else the deployed one, else the first GraySpace ships. */
  protected readonly chosenVersion = computed(() =>
    this.baseVersion() || this.offers()?.versions?.find(v => v.deployed)?.name || this.offers()?.versions?.[0]?.name || '');
  /** The scope a deploy uses: the one picked, else the deployed one, else tier1, the smaller base and the quickest to a working network. */
  protected readonly chosenScope = computed(() => this.baseScope() || this.offers()?.base?.version?.scope || 'tier1');
  /** The default host's image for an empty draft: the container image needing the least memory, else the first image. */
  protected readonly smallestImage = computed(() =>
    this.images().filter(i => i.type === 'lxc').sort((a, b) => (a.memory ?? 0) - (b.memory ?? 0))[0] ?? this.images()[0]);
  /** Deploy everything runs when nothing else is, and, with no base deployed, only for an admin on a GraySpace that runs base jobs. */
  protected readonly canDeployAll = computed(() =>
    this.connected() && !this.busy() && !this.quickRunning() && !this.running() && !this.baseRunning() &&
    (this.baseDeployed() || (this.admin() && !!this.server()?.base_jobs && !!this.chosenVersion())));

  /** Every place a site or an internet host can join the base: an ISP, a location, or an exact router. */
  protected readonly attachOptions = computed<AttachOption[]>(() => {
    const isps = this.capabilities()?.isps ?? [];
    const points = this.offers()?.attachmentPoints ?? [];
    const locations = [...new Set([...isps.flatMap(i => i.locations), ...points.map(p => p.location).filter((l): l is string => !!l)])];
    return [
      ...isps.map(i => ({ value: `isp:${i.name}`, label: `ISP: ${i.name}`, attach: { isp: i.name } })),
      ...locations.map(l => ({ value: `location:${l}`, label: `Location: ${l}`, attach: { location: l } })),
      ...points.map(p => ({ value: `router:${p.router}`, label: `Router: ${p.router} (${[p.isp, p.location].filter(Boolean).join(', ')})`, attach: { router: p.router } }))
    ];
  });

  /** The roles the enclave's hosts take that read variables, each with its variables. */
  protected readonly roleSettings = computed(() => {
    const used = new Set((this.spec()?.hosts ?? []).flatMap(h => h.roles ?? []));
    return this.roles()
      .filter(r => used.has(r.name) && Object.keys(r.variables ?? {}).length > 0)
      .map(r => ({ role: r.name, variables: Object.entries(r.variables!).map(([name, v]) => ({ name, ...v })) }));
  });

  private timer?: ReturnType<typeof setTimeout>;
  private baseTimer?: ReturnType<typeof setTimeout>;
  private destroyed = false;
  private connecting?: Subscription;

  constructor() {
    effect(() => {
      const id = this.store.scenarioId();
      if (id) {
        untracked(() => this.load(id));
      }
    });
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      clearTimeout(this.timer);
      clearTimeout(this.baseTimer);
      this.connecting?.unsubscribe();
    });
  }

  private load(id: number): void {
    this.api.getEnclave(id).subscribe({ next: e => this.enclave.set(e), error: e => this.draftError.set(errorMessage(e)) });
    this.api.getGraySpace().subscribe({
      next: s => {
        this.status.set(s);
        if (s.answering) {
          this.loadOffers();
          this.refresh();
          this.refreshBase();
        } else if (s.problem) {
          this.connectionError.set(s.problem);
        }
      },
      error: e => this.connectionError.set(errorMessage(e))
    });
  }

  private loadOffers(): void {
    this.api.getGraySpaceOffers().subscribe({
      next: o => {
        this.offers.set(o);
        this.connectionError.set(null);
      },
      error: e => this.connectionError.set(errorMessage(e))
    });
  }

  /** What GraySpace says of the last instance and job; again every 2 s while the job runs. */
  protected refresh(): void {
    clearTimeout(this.timer);
    this.api.followEnclave(this.store.scenarioId()).subscribe({
      next: f => {
        this.follow.set(f);
        if (f.job && !f.job.finished) {
          this.timer = setTimeout(() => this.refresh(), 2000);
        }
      },
      error: e => this.note.set(errorMessage(e))
    });
  }

  /** GraySpace's latest base job; again every 2 s while it runs, and the offers again once it finishes, for the base it left. */
  protected refreshBase(): void {
    clearTimeout(this.baseTimer);
    this.api.followBase().subscribe({
      next: f => {
        const wasRunning = this.baseRunning();
        this.baseFollow.set(f);
        if (f.job && !f.job.finished) {
          this.baseTimer = setTimeout(() => this.refreshBase(), 2000);
        } else if (wasRunning) {
          this.loadOffers();
        }
      },
      error: e => this.baseNote.set(errorMessage(e))
    });
  }

  /** An address that never answers holds a try until the API gives up, so a new press cancels it (the API cancels its call too). */
  protected connect(): void {
    this.connecting?.unsubscribe();
    const url = this.draftUrl().trim();
    this.busy.set('connect');
    this.connectingTo.set(url);
    this.connectionError.set(null);
    this.connecting = this.api.connectGraySpace(url, this.draftToken()).subscribe({
      next: s => {
        this.busy.set(null);
        this.connectingTo.set(null);
        this.status.set(s);
        this.draftToken.set('');
        this.loadOffers();
        this.refresh();
        this.refreshBase();
      },
      error: e => {
        this.busy.set(null);
        this.connectingTo.set(null);
        this.connectionError.set(errorMessage(e));
      }
    });
  }

  protected disconnect(): void {
    this.api.disconnectGraySpace().subscribe({
      next: () => {
        clearTimeout(this.timer);
        clearTimeout(this.baseTimer);
        this.status.set({ connected: false });
        this.offers.set(null);
        this.follow.set(null);
        this.baseFollow.set(null);
        this.preflight.set(null);
      },
      error: e => this.connectionError.set(errorMessage(e))
    });
  }

  /**
   * Deploys whatever is missing, in order, each with its default: the base (the chosen version and scope, checked
   * first) if none is deployed; then the enclave as drafted, or one NAT site with one segment and one host on the
   * smallest image if the draft is empty; then an instance of it, followed to the end. Pressed again, it carries on.
   */
  protected async deployAll(): Promise<void> {
    this.quickRunning.set(true);
    try {
      if (!this.baseDeployed()) {
        if (!this.baseRunning()) {
          const [version, scope] = [this.chosenVersion(), this.chosenScope()];
          this.quick.set(`Checking base ${version} (${scope})…`);
          const check = await this.call(this.api.getBasePreflight(version, scope));
          this.preflight.set(check);
          if (!check.ready) {
            this.quick.set('The base check found errors: see Base.');
            return;
          }
          this.quick.set(`Deploying base ${version} (${scope})…`);
          await this.call(this.api.baseAction('deploy', version, scope));
        }
        const job = await this.waitFor(() => this.api.followBase(), f => this.baseFollow.set(f), 'Deploying the base');
        if (job?.state !== 'succeeded') {
          this.quick.set(`The base deploy ${job?.state ?? 'stopped'}${job?.stage ? ` at ${job.stage}` : ''}: see Base for its log.`);
          return;
        }
        this.offers.set(await this.call(this.api.getGraySpaceOffers()));
      }

      const existing = this.follow()?.instance;
      if (existing && existing.state !== 'destroyed' && !this.running()) {
        this.quick.set(`This scenario's instance ${existing.id} is already ${existing.state}: see Deploy.`);
        return;
      }
      if (!existing || existing.state === 'destroyed') {
        const spec = this.spec();
        if (spec && !spec.sites?.length && !spec.hosts?.length) {
          this.quick.set('Drafting one site with one host…');
          await this.call(this.api.putEnclave(this.store.scenarioId(), this.defaultEnclave(spec)));
        }
        this.quick.set('Creating the instance…');
        this.enclave.set((await this.call(this.api.createInstance(this.store.scenarioId(), '', ''))).enclave);
      }
      const job = await this.waitFor(() => this.api.followEnclave(this.store.scenarioId()), f => this.follow.set(f), 'Creating the instance');
      const instance = this.follow()?.instance;
      this.quick.set(job?.state === 'succeeded'
        ? `Deployed: instance ${instance?.id} is ${instance?.state}.`
        : `The instance's job ${job?.state ?? 'stopped'}${job?.stage ? ` at ${job.stage}` : ''}: see Deploy for its log.`);
    } catch (e) {
      this.quick.set(errorMessage(e));
    } finally {
      this.quickRunning.set(false);
    }
  }

  /** The empty draft's default: one NAT site on the first attach point, with one segment, and one host on it. */
  private defaultEnclave(current: SliceSpec): SliceSpec {
    const spec = structuredClone(current);
    spec.sites = [{ name: 'site-1', attach: this.attachOptions()[0]?.attach ?? {}, segments: [{ name: 'lan' }] }];
    spec.hosts = [{ name: 'host-1', image: this.smallestImage()?.name ?? '', segment: 'lan', roles: [] }];
    this.enclave.update(e => e && { ...e, spec });
    return spec;
  }

  /** Follows a job until it finishes, every 2 s, showing where it is; its last state, or none when there's no job. */
  private async waitFor<T extends { job?: GsJob | null }>(follow: () => Observable<T>, seen: (f: T) => void, doing: string): Promise<GsJob | null | undefined> {
    for (;;) {
      const f = await firstValueFrom(follow());
      seen(f);
      if (!f.job || f.job.finished || this.destroyed) {
        return f.job;
      }
      this.quick.set(`${doing}: ${f.job.state}${f.job.stage ? ` at ${f.job.stage}` : ''}…`);
      await new Promise(done => setTimeout(done, 2000));
    }
  }

  /** One call of Deploy everything; every GraySpace button waits for it. */
  private async call<T>(call: Observable<T>): Promise<T> {
    this.busy.set('deploy-all');
    try {
      return await firstValueFrom(call);
    } finally {
      this.busy.set(null);
    }
  }

  protected setBaseVersion(value: string): void {
    this.baseVersion.set(value);
    this.preflight.set(null);
  }

  protected setBaseScope(value: string): void {
    this.baseScope.set(value);
    this.preflight.set(null);
  }

  protected checkBase(): void {
    this.baseStep('preflight', this.api.getBasePreflight(this.chosenVersion(), this.chosenScope()), p => this.preflight.set(p));
  }

  protected deployBase(): void {
    this.baseStep('deploy-base', this.api.baseAction('deploy', this.chosenVersion(), this.chosenScope()), () => {
      this.preflight.set(null);
      this.refreshBase();
    });
  }

  protected configureBase(): void {
    this.baseStep('configure-base', this.api.baseAction('configure'), () => this.refreshBase());
  }

  protected destroyBase(): void {
    if (!confirm('Destroy the base on GraySpace? Its routers and core services are taken down for everyone. GraySpace refuses while any instance is live.')) {
      return;
    }
    this.baseStep('destroy-base', this.api.destroyBase(), () => this.refreshBase());
  }

  protected baseJob(action: 'cancel' | 'retry'): void {
    this.baseStep(`base-${action}`, this.api.baseJobAction(action), () => this.refreshBase());
  }

  /** Runs one base step; a refusal's message shows under Base. */
  private baseStep<T>(name: string, call: Observable<T>, done: (answer: T) => void): void {
    this.busy.set(name);
    this.baseNote.set(null);
    call.subscribe({
      next: answer => {
        this.busy.set(null);
        done(answer);
      },
      error: e => {
        this.busy.set(null);
        this.baseNote.set(errorMessage(e));
      }
    });
  }

  /** Changes the draft and saves it to the builder. Nothing goes to GraySpace until a deploy step. */
  private edit(change: (spec: SliceSpec) => void): void {
    const current = this.spec();
    if (!current) {
      return;
    }
    const spec = structuredClone(current);
    change(spec);
    this.enclave.update(e => e && { ...e, spec });
    this.api.putEnclave(this.store.scenarioId(), spec).subscribe({ next: () => this.draftError.set(null), error: e => this.draftError.set(errorMessage(e)) });
  }

  protected setField(field: 'slug' | 'prefix' | 'owner', value: string): void {
    this.edit(s => {
      s[field] = value.trim();
    });
  }

  protected reuse(): void {
    this.step('reuse', this.api.reuseEnclave(this.store.scenarioId(), this.reuseSlug()), () => this.note.set(`This scenario now uses ${this.reuseSlug()}.`));
  }

  protected addSite(): void {
    this.edit(s => (s.sites ??= []).push({
      name: `site-${s.sites.length + 1}`, attach: this.attachOptions()[0]?.attach ?? {}, segments: [{ name: `segment-${this.segments().length + 1}` }]
    }));
  }

  protected removeSite(site: number): void {
    this.edit(s => s.sites!.splice(site, 1));
  }

  protected setSiteName(site: number, value: string): void {
    this.edit(s => {
      s.sites![site].name = value.trim();
    });
  }

  protected setAttach(site: number, value: string): void {
    this.edit(s => {
      s.sites![site].attach = this.attachOf(value);
    });
  }

  protected setRouted(site: number, value: string): void {
    this.edit(s => {
      s.sites![site].routed = value === 'routed';
    });
  }

  protected addSegment(site: number): void {
    this.edit(s => s.sites![site].segments.push({ name: `segment-${this.segments().length + 1}` }));
  }

  protected removeSegment(site: number, segment: number): void {
    this.edit(s => s.sites![site].segments.splice(segment, 1));
  }

  /** A blank or auto CIDR is left out, so GraySpace allocates one from its pool. */
  protected setSegment(site: number, segment: number, field: 'name' | 'cidr', value: string): void {
    this.edit(s => {
      const target = s.sites![site].segments[segment];
      const v = value.trim();
      if (field === 'cidr' && (!v || v === 'auto')) {
        delete target.cidr;
      } else {
        target[field] = v;
      }
    });
  }

  /** An attach's select value: its router, else its location, else its ISP. */
  protected attachValue(attach: GsAttach | null | undefined): string {
    return attach?.router ? `router:${attach.router}` : attach?.location ? `location:${attach.location}` : attach?.isp ? `isp:${attach.isp}` : '';
  }

  private attachOf(value: string): GsAttach {
    return this.attachOptions().find(o => o.value === value)?.attach ?? {};
  }

  protected addHost(): void {
    this.edit(s => (s.hosts ??= []).push({ name: `host-${s.hosts.length + 1}`, image: this.images()[0]?.name ?? '', segment: this.segments()[0]?.name ?? null, roles: [] }));
  }

  protected removeHost(host: number): void {
    this.edit(s => s.hosts!.splice(host, 1));
  }

  protected setHostName(host: number, value: string): void {
    this.edit(s => {
      s.hosts![host].name = value.trim();
    });
  }

  /** A new image drops the roles it can't take, and the size goes back to the image's. */
  protected setImage(host: number, value: string): void {
    this.edit(s => {
      const h = s.hosts![host];
      h.image = value;
      h.size = null;
      h.roles = (h.roles ?? []).filter(r => this.rolesFor(h).some(role => role.name === r));
    });
  }

  /** A host is on one of the enclave's segments, or directly on the internet at an attach point. */
  protected placementValue(h: GsHost): string {
    return h.internet ? `internet:${this.attachValue(h.internet)}` : h.segment ? `segment:${h.segment}` : '';
  }

  protected setPlacement(host: number, value: string): void {
    this.edit(s => {
      const h = s.hosts![host];
      const internet = value.startsWith('internet:');
      h.internet = internet ? this.attachOf(value.slice('internet:'.length)) : null;
      h.segment = internet ? null : value.slice('segment:'.length);
    });
  }

  /** Blank is one host, named as it is; more are named name-01, name-02… */
  protected setCount(host: number, value: string): void {
    this.edit(s => {
      const n = parseInt(value, 10);
      s.hosts![host].count = n > 0 ? n : null;
    });
  }

  /** GraySpace takes a whole size or none: a blank field is the image's, and the image's own size is none. */
  protected setSize(host: number, field: 'cores' | 'memory' | 'disk', value: string): void {
    this.edit(s => {
      const h = s.hosts![host];
      const image = this.imageOf(h.image);
      const size = { cores: h.size?.cores ?? image?.cores ?? 0, memory: h.size?.memory ?? image?.memory ?? 0, disk: h.size?.disk ?? image?.disk ?? 0 };
      const n = parseInt(value, 10);
      size[field] = n > 0 ? n : image?.[field] ?? 0;
      h.size = image && size.cores === image.cores && size.memory === image.memory && size.disk === image.disk ? null : size;
    });
  }

  protected toggleRole(host: number, role: string, on: boolean): void {
    this.edit(s => {
      const h = s.hosts![host];
      h.roles = on ? [...new Set([...(h.roles ?? []), role])] : (h.roles ?? []).filter(r => r !== role);
    });
  }

  protected setPublish(host: number, on: boolean): void {
    this.edit(s => {
      s.hosts![host].publish = on;
    });
  }

  /** The GHOSTS simulated-users pool a host belongs to, as its ghosts.pool label; blank removes it. */
  protected setPool(host: number, value: string): void {
    this.edit(s => {
      const h = s.hosts![host];
      const labels = { ...(h.labels ?? {}) };
      if (value.trim()) {
        labels['ghosts.pool'] = value.trim();
      } else {
        delete labels['ghosts.pool'];
      }
      h.labels = labels;
    });
  }

  protected setRoleVar(role: string, name: string, value: string): void {
    this.edit(s => {
      const vars = { ...(s.role_vars?.[role] ?? {}) };
      if (value.trim()) {
        vars[name] = value.trim();
      } else {
        delete vars[name];
      }
      s.role_vars = { ...(s.role_vars ?? {}), [role]: vars };
    });
  }

  protected imageOf(name: string) {
    return this.images().find(i => i.name === name);
  }

  /** The roles a host's image can take; every role when GraySpace doesn't say the image's OS. */
  protected rolesFor(h: GsHost): GsRole[] {
    const os = this.imageOf(h.image)?.os_family;
    return os ? this.roles().filter(r => r.os_families.includes(os)) : this.roles();
  }

  protected requiredVariables(role: GsRole): string[] {
    return Object.entries(role.variables ?? {}).filter(([, v]) => v.required).map(([name]) => name);
  }

  protected validate(): void {
    this.step('validate', this.api.validateEnclave(this.store.scenarioId()), (a: EnclaveAnswer<GsValidateResult>) => {
      this.findings.set(a.graySpace?.findings ?? []);
      this.note.set(a.graySpace?.valid ? 'GraySpace finds it valid.' : 'Not valid yet: see the findings.');
    });
  }

  protected save(): void {
    this.step('save', this.api.saveEnclave(this.store.scenarioId()), (a: EnclaveAnswer<GsPutSliceResult>) => {
      this.findings.set(a.graySpace?.findings ?? []);
      this.note.set(`Saved to GraySpace as version ${this.short(a.graySpace?.version)}${a.graySpace?.created ? '' : ', the same as before'}.`);
      this.loadOffers();
    });
  }

  protected plan(): void {
    this.step('plan', this.api.planEnclave(this.store.scenarioId()), () => {
      this.loadOffers();
      this.refresh();
    });
  }

  protected create(): void {
    this.step('create', this.api.createInstance(this.store.scenarioId(), this.instancePrefix().trim(), this.instanceVersion()), () => {
      this.loadOffers();
      this.refresh();
    });
  }

  protected job(action: 'cancel' | 'retry'): void {
    this.step(action, this.api.jobAction(this.store.scenarioId(), action), () => this.refresh());
  }

  protected destroy(): void {
    if (!confirm(`Destroy instance ${this.enclave()?.instanceId} on GraySpace? Its hosts are taken down; GraySpace keeps the record.`)) {
      return;
    }
    this.step('destroy', this.api.destroyInstance(this.store.scenarioId()), () => this.refresh());
  }

  /** Runs one GraySpace step: the enclave comes back with it; a refusal's message and findings show under Deploy. */
  private step<T extends EnclaveAnswer>(name: string, call: Observable<T>, done: (answer: T) => void): void {
    this.busy.set(name);
    this.note.set(null);
    this.findings.set(null);
    call.subscribe({
      next: answer => {
        this.busy.set(null);
        this.enclave.set(answer.enclave);
        done(answer);
      },
      error: e => {
        this.busy.set(null);
        this.note.set(errorMessage(e));
        const findings = e instanceof HttpErrorResponse ? e.error?.detail?.findings : undefined;
        this.findings.set(Array.isArray(findings) ? findings : null);
      }
    });
  }

  /** A version's first characters, without sha256:. */
  protected short(version?: string | null): string {
    return version ? version.replace(/^sha256:/, '').slice(0, 12) : '';
  }

  protected hostnames(servers: GsServer[]): string {
    return servers.map(s => s.hostname).join(', ');
  }

  protected gib(bytes?: number | null): string {
    return bytes ? `${Math.round(bytes / 2 ** 30)} GiB` : '?';
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

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
