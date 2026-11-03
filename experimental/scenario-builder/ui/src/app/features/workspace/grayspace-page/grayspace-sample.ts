// What a GraySpace offers, as its Terrain API 1.3.0 reports it, copied from GHOSTS's GraySpace simulator
// (src/Ghosts.Api/Infrastructure/Terrain/GraySpaceSimulator.cs). The GraySpace screen shows this until the
// builder connects to a real GraySpace.

export const GRAYSPACE_SAMPLE = {
  /** GET /capabilities */
  jobs: true,
  features: ['nat-site', 'published-host', 'internet-host'],
  managementSubnet: '10.0.0.0/16',
  pool: '192.168.0.0/16',

  /** GET /base */
  base: { version: 'grayspace/2026.10', scope: 'tier1', status: 'healthy', routers: '3 of 3 running', bgp: '4 of 4 established' },
  /** GET /base/preflight: the checks it runs, none run here. */
  preflight: ['proxmox-api', 'storage', 'bridges'],

  /** GET /attachment-points */
  attachmentPoints: [
    { router: 'lum-chi', isp: 'Lumen', asn: 3356, location: 'Chicago' },
    { router: 'are-ash', isp: 'Arelion', asn: 1299, location: 'Ashburn' },
    { router: 'lum-ash', isp: 'Lumen', asn: 3356, location: 'Ashburn' }
  ],

  /** GET /images */
  images: [
    { name: 'alpine-3.22', type: 'lxc', os: 'alpine', description: 'Alpine Linux 3.22' },
    { name: 'ubuntu-22.04-lxc', type: 'lxc', os: 'ubuntu', description: 'Ubuntu 22.04 container' },
    { name: 'ubuntu-24.04-vm', type: 'vm', os: 'ubuntu', description: 'Ubuntu 24.04 cloud image VM' },
    { name: 'ghosts-pandora', type: 'oci', os: 'oci', description: 'GHOSTS Pandora web and social media server' }
  ],

  /** GET /roles, with the role_vars each one needs. */
  roles: [
    { name: 'web', description: 'nginx with a placeholder page', os: ['ubuntu', 'debian'], needs: [] as string[] },
    { name: 'ghosts-client', description: 'A GHOSTS client installed from client_url, reporting to api_url', os: ['ubuntu', 'debian', 'windows'], needs: ['client_url', 'api_url'] },
    { name: 'mail', description: 'Postfix and Dovecot', os: ['ubuntu', 'debian'], needs: [] as string[] },
    { name: 'dns', description: 'BIND for <prefix>.internal', os: ['ubuntu', 'debian'], needs: [] as string[] },
    { name: 'file', description: 'A Samba share', os: ['ubuntu', 'debian'], needs: [] as string[] },
    { name: 'ad-dc', description: 'A Samba AD domain controller', os: ['ubuntu', 'debian'], needs: ['realm', 'domain', 'admin_password'] }
  ],

  /** GET /capabilities: dns and cas */
  dns: { tlds: ['com', 'org', 'net'], hosting: ['dns-cloudflare'], resolvers: ['dns-resolver-chi'], recordTypes: ['A', 'CNAME', 'TXT', 'MX'] },
  cas: [{ hostname: 'ca-graytrust', name: 'GrayTrust' }],

  /** GET /map: the base's links */
  links: [
    { id: 'lum-chi:are-ash', type: 'Peers with' },
    { id: 'lum-chi:lum-ash', type: 'Internal link' }
  ],

  /** A create job's stages, and the actions a ready instance takes (POST /instances/{id}/actions). */
  createStages: ['validate', 'allocate', 'render', 'apply', 'wait', 'configure', 'dns', 'conditions', 'check', 'ready'],
  actions: ['link.condition', 'link.clear', 'link.down', 'link.up', 'dns.record.set', 'dns.record.delete', 'host.start', 'host.stop']
};
