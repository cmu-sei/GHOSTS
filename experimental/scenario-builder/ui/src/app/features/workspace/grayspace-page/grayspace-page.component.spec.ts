import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AnsweredView, Me, ScenarioType, ScenarioTypeQuestion } from '../../../core/models/builder.model';
import { BaseJobView, EnclaveView, FollowView, GraySpaceOffers, GraySpaceStatus, SliceSpec } from '../../../core/models/grayspace.model';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { WorkspaceStore } from '../workspace.store';
import { GraySpacePageComponent } from './grayspace-page.component';

const question = (id: string, label: string): ScenarioTypeQuestion => ({ id, label, text: `${label}?`, why: 'w', answer: 'list', path: `/network/${id}` });

const scenarioType: ScenarioType = {
  id: 'small-unit-team-exercise', version: '1', name: 'Small Unit Team Exercise', drafts: [], topology: true,
  stages: [{ number: 0, title: 'Network and Range', questions: [question('segments', 'Network Segments'), question('connectivity', 'Connectivity')] }]
};

const answered: AnsweredView = {
  id: 'segments', stage: 0, value: ['IT Network: Corporate services', 'DMZ: Jump hosts'], who: 'stated', by: 'dev', via: 'person', time: '2026-11-02T00:00:00Z'
};

const newSpec = (): SliceSpec => ({ apiVersion: 'grayspace/v1', kind: 'Slice', slug: 'cyber-antigen', prefix: 'cyberant', owner: 'scenario-builder', sites: [], hosts: [], role_vars: {} });

const offers: GraySpaceOffers = {
  server: {
    jobs: true, base_jobs: true, slice_features: ['nat-site', 'internet-host'], management_subnet: '10.0.0.0/16', slice_pool: '192.168.0.0/16',
    images: [{ name: 'ubuntu-24.04-vm', type: 'vm', os_family: 'ubuntu', description: 'Ubuntu 24.04', cores: 2, memory: 2048, disk: 20 }],
    roles: [
      { name: 'web', description: 'nginx', os_families: ['ubuntu'] },
      {
        name: 'ghosts-client', description: 'A GHOSTS client', os_families: ['ubuntu', 'windows'],
        variables: { client_url: { description: 'Where the client archive is', required: true }, api_url: { description: 'The GHOSTS API', required: true } }
      }
    ],
    capacity: null
  },
  capabilities: {
    dns: { tlds: ['com'], hosting: [{ hostname: 'dns-cloudflare' }], resolvers: [{ hostname: 'dns-resolver-chi' }] },
    cas: [{ hostname: 'ca-graytrust', name: 'GrayTrust' }],
    isps: [{ name: 'Lumen', asn: 3356, locations: ['Chicago'] }]
  },
  base: { version: { name: 'grayspace/2026.10', scope: 'tier1' }, status: 'healthy', routers: { expected: 3, running: 3 }, bgp: { total: 4, established: 4 } },
  map: { features: [{ id: 'lum-chi:are-ash', properties: { kind: 'link', type: 'PEERS_WITH', state: 'up' } }] },
  slices: [],
  problems: { attachmentPoints: 'GraySpace refused it: Not Found' }
};

const failedJob: FollowView = {
  enclave: { spec: newSpec(), version: 'sha256:abcdef0123456789', instanceId: 'cyberant', jobId: 'job-1' },
  instance: { id: 'cyberant', state: 'failed' },
  job: { id: 'job-1', kind: 'create', state: 'failed', stage: 'apply', finished: '2026-11-06T10:00:00Z', error: 'terraform apply failed' },
  log: 'validate ok\napply failed\n'
};

async function render(
  status: GraySpaceStatus = { connected: true, url: 'http://gs.local:8000', connectedBy: 'dev', since: '2026-11-06T09:00:00Z' }, me: Me = { user: 'dev', admin: true },
  shown: GraySpaceOffers = offers
) {
  let enclave: EnclaveView = { spec: newSpec() };
  const api = {
    getGraySpace: vi.fn(() => of(status)),
    connectGraySpace: vi.fn((url: string): Observable<GraySpaceStatus> => of({ connected: true, url, connectedBy: 'dev', since: '2026-11-06T09:00:00Z' })),
    disconnectGraySpace: vi.fn(() => of(undefined)),
    getGraySpaceOffers: vi.fn(() => of(shown)),
    getEnclave: vi.fn(() => of(enclave)),
    putEnclave: vi.fn((_: number, spec: SliceSpec) => of(enclave = { ...enclave, spec })),
    reuseEnclave: vi.fn(),
    validateEnclave: vi.fn(() => of({
      enclave, graySpace: { valid: false, version: 'sha256:aa', findings: [{ code: 'NO_SITES', severity: 'warning', path: 'sites', message: 'The enclave has no sites' }] }
    })),
    saveEnclave: vi.fn(() => of({ enclave: { ...enclave, version: 'sha256:abcdef0123456789' }, graySpace: { slug: 'cyber-antigen', version: 'sha256:abcdef0123456789', created: true, findings: [] } })),
    planEnclave: vi.fn(),
    createInstance: vi.fn(() => of({ enclave: failedJob.enclave, graySpace: { instance: 'cyberant', job: { id: 'job-1' } } })),
    followEnclave: vi.fn(() => of<FollowView>({ enclave })),
    destroyInstance: vi.fn(),
    jobAction: vi.fn(),
    getBasePreflight: vi.fn(() => of({ ready: true, version: 'grayspace/2026.11', scope: 'tier1', checks: [{ name: 'proxmox-api', status: 'ok', message: 'Proxmox answers' }] })),
    baseAction: vi.fn(() => of({ job: { id: 'job-b1' } })),
    destroyBase: vi.fn(() => of([])),
    followBase: vi.fn((): Observable<BaseJobView> => of({})),
    baseJobAction: vi.fn(() => of({}))
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: BuilderApiService, useValue: api },
      { provide: MeService, useValue: { me: signal(me) } },
      {
        provide: WorkspaceStore,
        useValue: { scenarioId: signal(7), scenarioType: signal(scenarioType), name: signal('Cyber Antigen'), answer: (id: string) => id === 'segments' ? answered : undefined }
      }
    ]
  });
  const fixture = TestBed.createComponent(GraySpacePageComponent);
  await fixture.whenStable();
  return { fixture, api, root: fixture.nativeElement as HTMLElement };
}

const section = (root: HTMLElement, title: string) =>
  Array.from(root.querySelectorAll<HTMLElement>('section.decision')).find(s => s.querySelector('h2')?.textContent?.trim() === title)!;

const button = (root: HTMLElement, text: string) =>
  Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.trim() === text)!;

function type(input: HTMLInputElement | HTMLSelectElement, value: string, event = 'change'): void {
  input.value = value;
  input.dispatchEvent(new Event(event));
}

describe('GraySpacePageComponent', () => {
  it('lays out every GraySpace decision in order, with the network answers in the section they feed', async () => {
    const { root } = await render();

    expect(Array.from(root.querySelectorAll('section.decision h2')).map(h => h.textContent?.trim())).toEqual([
      'Connection', 'Base', 'Enclave', 'Sites and Segments', 'Hosts', 'Role Settings', 'DNS and Certificates', 'Network Conditions', 'Deploy'
    ]);
    const sites = section(root, 'Sites and Segments').querySelector('.answers')!;
    expect(sites.textContent).toContain('IT Network: Corporate services');
    expect(sites.textContent).toContain('No answer yet');
    expect(section(root, 'Connection').querySelector('.answers')).toBeNull();
  });

  it('shows what the connected GraySpace offers, and why a part is missing', async () => {
    const { root } = await render();

    expect(root.querySelector('.pill')?.textContent).toContain('Connected to http://gs.local:8000');
    expect(section(root, 'Connection').textContent).toContain('192.168.0.0/16');
    expect(section(root, 'Base').textContent).toContain('3 of 3 running');
    expect(section(root, 'Hosts').textContent).toContain('ubuntu-24.04-vm');
    expect(section(root, 'Role Settings').textContent).toContain('client_url');
    expect(section(root, 'DNS and Certificates').textContent).toContain('GrayTrust');
    expect(section(root, 'Network Conditions').textContent).toContain('lum-chi:are-ash');
    expect(section(root, 'Sites and Segments').textContent).toContain('GraySpace refused it: Not Found');
  });

  it('with no base deployed, shows what the server has, and why DNS and certificates are missing', async () => {
    const noBase = 'GraySpace refused it: The deployed base version is unknown or not a shipped version; name one with ?version=';
    const { root } = await render(undefined, undefined, { server: offers.server, problems: { capabilities: noBase } });

    expect(section(root, 'Connection').textContent).toContain('192.168.0.0/16');
    expect(section(root, 'Hosts').textContent).toContain('ubuntu-24.04-vm');
    expect(section(root, 'DNS and Certificates').textContent).toContain(noBase);
  });

  it('leaves connecting to an admin, and waits for a connection to deploy', async () => {
    const { root, api } = await render({ connected: false }, { user: 'lee', admin: false });

    expect(root.querySelector('.pill')?.textContent).toContain('Not connected to GraySpace');
    expect(section(root, 'Connection').textContent).toContain('An admin connects GraySpace here.');
    expect(section(root, 'Connection').querySelector('form')).toBeNull();
    expect(button(root, 'Validate').disabled).toBe(true);
    expect(button(root, 'Create instance').disabled).toBe(true);
    expect(api.getGraySpaceOffers).not.toHaveBeenCalled();
  });

  it('connects as an admin, then loads the offers and forgets the typed token', async () => {
    const { fixture, root, api } = await render({ connected: false });
    const [address, token] = Array.from(section(root, 'Connection').querySelectorAll<HTMLInputElement>('input'));

    type(address, 'http://gs.local:8000', 'input');
    type(token, 's3cret', 'input');
    await fixture.whenStable();
    button(root, 'Connect').click();
    await fixture.whenStable();

    expect(api.connectGraySpace).toHaveBeenCalledWith('http://gs.local:8000', 's3cret');
    expect(api.getGraySpaceOffers).toHaveBeenCalled();
    expect(root.querySelector('.pill')?.textContent).toContain('Connected to http://gs.local:8000');
    expect((section(root, 'Connection').querySelectorAll<HTMLInputElement>('input')[1]).value).toBe('');
  });

  it('shows a spinner while connecting, and Connect can be pressed again to try again', async () => {
    const { fixture, root, api } = await render({ connected: false });
    const attempts: Subject<GraySpaceStatus>[] = [];
    api.connectGraySpace.mockImplementation(() => {
      attempts.push(new Subject<GraySpaceStatus>());
      return attempts.at(-1)!;
    });
    const [address, token] = Array.from(section(root, 'Connection').querySelectorAll<HTMLInputElement>('input'));
    type(address, 'http://10.255.255.1:8000', 'input');
    type(token, 's3cret', 'input');
    await fixture.whenStable();

    button(root, 'Connect').click();
    await fixture.whenStable();
    expect(section(root, 'Connection').querySelector('.connecting .spinner')).not.toBeNull();
    expect(section(root, 'Connection').querySelector('.connecting')?.textContent).toContain('Connecting to http://10.255.255.1:8000');
    expect(button(root, 'Connect').disabled).toBe(false);

    type(address, 'http://gs.local:8000', 'input');
    await fixture.whenStable();
    button(root, 'Connect').click();
    await fixture.whenStable();
    expect(api.connectGraySpace).toHaveBeenLastCalledWith('http://gs.local:8000', 's3cret');
    expect(attempts[0].observed).toBe(false);

    attempts[1].error(new HttpErrorResponse({ status: 502, error: { code: 'GRAYSPACE_UNREACHABLE', message: 'GraySpace at http://gs.local:8000 didn’t answer.' } }));
    await fixture.whenStable();
    expect(section(root, 'Connection').querySelector('.connecting')).toBeNull();
    expect(section(root, 'Connection').querySelector('.error')?.textContent).toContain('didn’t answer');
    expect(button(root, 'Connect').disabled).toBe(false);
  });

  it('builds the enclave as a draft: sites, segments, hosts, roles and their settings', async () => {
    const { fixture, root, api } = await render();

    button(root, 'Add site').click();
    await fixture.whenStable();
    button(root, 'Add host').click();
    await fixture.whenStable();
    const client = Array.from(section(root, 'Hosts').querySelectorAll<HTMLInputElement>('input[type="checkbox"]'))
      .find(c => c.parentElement?.textContent?.includes('ghosts-client'))!;
    client.checked = true;
    client.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const clientUrl = Array.from(section(root, 'Role Settings').querySelectorAll<HTMLLabelElement>('label')).find(l => l.textContent?.includes('ghosts-client.client_url'))!;
    type(clientUrl.querySelector('input')!, 'http://ghosts:5000/client.zip');
    await fixture.whenStable();

    const spec = api.putEnclave.mock.lastCall![1] as SliceSpec;
    expect(spec.sites).toEqual([{ name: 'site-1', attach: { isp: 'Lumen' }, segments: [{ name: 'segment-1' }] }]);
    expect(spec.hosts).toEqual([{ name: 'host-1', image: 'ubuntu-24.04-vm', segment: 'segment-1', roles: ['ghosts-client'] }]);
    expect(spec.role_vars).toEqual({ 'ghosts-client': { client_url: 'http://ghosts:5000/client.zip' } });
    expect(api.validateEnclave).not.toHaveBeenCalled();
  });

  it('validates and saves to GraySpace, showing the findings and the version', async () => {
    const { fixture, root, api } = await render();

    button(root, 'Validate').click();
    await fixture.whenStable();
    expect(api.validateEnclave).toHaveBeenCalledWith(7);
    expect(root.querySelector('.findings')?.textContent).toContain('The enclave has no sites');

    button(root, 'Save').click();
    await fixture.whenStable();
    expect(root.querySelector('.note')?.textContent).toContain('Saved to GraySpace as version abcdef012345');
    expect(section(root, 'Enclave').textContent).toContain('abcdef012345');
  });

  it('creates an instance and follows its job, offering a retry when it fails', async () => {
    const { fixture, root, api } = await render();
    api.followEnclave.mockReturnValue(of(failedJob));

    button(root, 'Create instance').click();
    await fixture.whenStable();

    expect(api.createInstance).toHaveBeenCalledWith(7, '', '');
    const deploy = section(root, 'Deploy');
    expect(deploy.textContent).toContain('create job job-1: failed at apply');
    expect(deploy.textContent).toContain('terraform apply failed');
    expect(deploy.querySelector('.log')?.textContent).toContain('apply failed');
    expect(button(root, 'Retry').disabled).toBe(false);
    expect(button(root, 'Cancel').disabled).toBe(true);
    expect(button(root, 'Destroy instance').disabled).toBe(false);
  });

  it('shows a refused create’s message and GraySpace’s findings', async () => {
    const { fixture, root, api } = await render();
    api.createInstance.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 422,
      error: {
        code: 'GRAYSPACE_REFUSED', message: 'GraySpace refused it: 1 finding(s): hosts[0].image: unknown image',
        detail: { findings: [{ code: 'IMAGE_UNKNOWN', severity: 'error', path: 'hosts[0].image', message: 'unknown image' }] }
      }
    })));

    button(root, 'Create instance').click();
    await fixture.whenStable();

    expect(root.querySelector('.note')?.textContent).toContain('GraySpace refused it');
    expect(root.querySelector('.findings tr.is-error')?.textContent).toContain('hosts[0].image');
  });

  it('lets an admin check a base version, deploy it once the check passes, and follow its job', async () => {
    const versions = [{ name: 'grayspace/2026.10', deployed: true }, { name: 'grayspace/2026.11', deployed: false }];
    const { fixture, root, api } = await render(undefined, undefined, { ...offers, versions });
    const base = section(root, 'Base');
    expect(button(root, 'Deploy base').disabled).toBe(true);

    type(base.querySelector<HTMLSelectElement>('select[aria-label="Base version"]')!, 'grayspace/2026.11');
    type(base.querySelector<HTMLSelectElement>('select[aria-label="Base scope"]')!, 'tier1');
    button(root, 'Check').click();
    await fixture.whenStable();

    expect(api.getBasePreflight).toHaveBeenCalledWith('grayspace/2026.11', 'tier1');
    expect(base.textContent).toContain('Proxmox answers');
    api.followBase.mockReturnValue(of({ job: { id: 'job-b1', kind: 'deploy_base', state: 'running', stage: 'terraform' }, log: 'terraform apply\n' }));
    button(root, 'Deploy base').click();
    await fixture.whenStable();

    expect(api.baseAction).toHaveBeenCalledWith('deploy', 'grayspace/2026.11', 'tier1');
    expect(base.textContent).toContain('deploy_base job job-b1: running at terraform');
    expect(base.querySelector('.log')?.textContent).toContain('terraform apply');
    expect(button(root, 'Configure base').disabled).toBe(true);
    expect(button(root, 'Cancel base job').disabled).toBe(false);
  });

  it('asks before destroying the base, and shows GraySpace’s refusal while instances are live', async () => {
    const { fixture, root, api } = await render();
    const asked = vi.spyOn(window, 'confirm').mockReturnValue(true);
    api.destroyBase.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { code: 'GRAYSPACE_REFUSED', message: 'GraySpace refused it: 2 slice instance(s) are live (harborbl, mer); destroy them first' }
    })));

    button(root, 'Destroy base').click();
    await fixture.whenStable();

    expect(asked).toHaveBeenCalled();
    expect(section(root, 'Base').querySelector('.note')?.textContent).toContain('harborbl, mer');
    asked.mockRestore();
  });

  it('shows everyone the base, and leaves changing it to an admin', async () => {
    const { root } = await render(undefined, { user: 'lee', admin: false });
    const base = section(root, 'Base');

    expect(base.textContent).toContain('3 of 3 running');
    expect(base.textContent).toContain('An admin deploys, configures or destroys the base.');
    expect(base.querySelector('button')).toBeNull();
  });

  describe('Deploy everything', () => {
    const alpine = { name: 'alpine-3.22', type: 'lxc', os_family: 'alpine', cores: 1, memory: 256, disk: 4 };
    const server = { ...offers.server, images: [...offers.server.images, alpine] };
    const nothing: GraySpaceOffers = {
      server, versions: [{ name: 'grayspace/2026.10', deployed: false }],
      base: { version: null, status: 'not_deployed', routers: { expected: 0 }, bgp: {} }, problems: {}
    };
    const ready: FollowView = {
      enclave: failedJob.enclave, instance: { id: 'cyberant', state: 'ready' },
      job: { id: 'job-1', kind: 'create_instance', state: 'succeeded', finished: '2026-11-06T21:10:00Z' }
    };
    const quick = (root: HTMLElement) => root.querySelector('.quick')?.textContent ?? '';

    it('from nothing: checks and deploys the default base, drafts one site and host, and creates the instance', async () => {
      const { root, api } = await render(undefined, undefined, nothing);
      api.getGraySpaceOffers.mockReturnValue(of({ ...offers, server }));
      api.followBase.mockReturnValue(of({ job: { id: 'job-b1', kind: 'deploy_base', state: 'succeeded', finished: '2026-11-06T21:00:00Z' } }));
      api.followEnclave.mockReturnValue(of(ready));

      button(root, 'Deploy everything').click();
      await vi.waitFor(() => expect(quick(root)).toContain('Deployed'));

      expect(api.getBasePreflight).toHaveBeenCalledWith('grayspace/2026.10', 'tier1');
      expect(api.baseAction).toHaveBeenCalledWith('deploy', 'grayspace/2026.10', 'tier1');
      const spec = api.putEnclave.mock.calls.at(-1)![1];
      expect(spec.sites).toEqual([{ name: 'site-1', attach: { isp: 'Lumen' }, segments: [{ name: 'lan' }] }]);
      expect(spec.hosts).toEqual([{ name: 'host-1', image: 'alpine-3.22', segment: 'lan', roles: [] }]);
      expect(api.createInstance).toHaveBeenCalledWith(7, '', '');
      expect(quick(root)).toContain('instance cyberant is ready');
    });

    it('with a base deployed, goes straight to the enclave and its instance', async () => {
      const { root, api } = await render();
      api.followEnclave.mockReturnValue(of(ready));

      button(root, 'Deploy everything').click();
      await vi.waitFor(() => expect(quick(root)).toContain('Deployed'));

      expect(api.getBasePreflight).not.toHaveBeenCalled();
      expect(api.baseAction).not.toHaveBeenCalled();
      expect(api.createInstance).toHaveBeenCalledWith(7, '', '');
    });

    it('stops at a failed base deploy, says where, and creates nothing', async () => {
      const { root, api } = await render(undefined, undefined, nothing);
      api.followBase.mockReturnValue(of({
        job: { id: 'job-b1', kind: 'deploy_base', state: 'failed', stage: 'init', error: 'terraform init -input=false exited 2', finished: '2026-11-06T21:00:00Z' }
      }));

      button(root, 'Deploy everything').click();
      await vi.waitFor(() => expect(quick(root)).toContain('failed at init'));

      expect(api.createInstance).not.toHaveBeenCalled();
      expect(api.putEnclave).not.toHaveBeenCalled();
    });
  });
});
