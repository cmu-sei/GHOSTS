// What an Updraft offers, as the README of its lab author CLI (updraft/cli on Bitbucket) describes it. The
// Updraft screen shows this until the builder can run the CLI. None of it is a real lab, image or host: those
// are only known once it's connected.

export const UPDRAFT_SAMPLE = {
  /** ~/.updraft/env, written by the workspace template; real environment variables win. */
  giteaUrl: 'https://git.updraft.clouddev-sei.org',
  /** Where lab new and lab open clone, as <owner>/<repo>. */
  labsDir: '~/labs',
  /** Where the Gitea token comes from. It never goes on argv or stdout. */
  token: ['coder external-auth access-token gitea', '$GITEA_TOKEN'],

  /** lab new generates the repo from this one. */
  labTemplate: 'updraft/lab-template',

  /** image new --from: the public aliases, each an SSM parameter. */
  publicImages: [
    { alias: 'ubuntu2404', windows: false },
    { alias: 'al2023', windows: false },
    { alias: 'win2022', windows: true },
    { alias: 'win2025', windows: true }
  ],
  /** host list resolves an image here, in this order. */
  resolution: ['the library', 'this lab’s own AMI', 'public, through SSM'],
  /** image build names the AMI this way. */
  amiName: 'updraft/<org>/<repo>/<image>-v<N>',

  /** host add takes the next free number, these first. */
  hostNumbers: [10, 20],
  /** host clone adds at most this many zones. */
  cloneZones: 4,

  /** deploy, status, down and grant act on this projection unless --name says otherwise. */
  projection: 'dev',
  /** How deploy exits when the slot broker refuses. */
  refusals: [
    { exit: 3, reason: 'Refused for capacity' },
    { exit: 4, reason: 'Slot still in use' }
  ]
};
