<div>

# GHOSTS NPC Framework

**Realistic User Behavior Simulation for Cyber Training, Exercises, and Research**

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-8.2-green.svg)](https://github.com/cmu-sei/GHOSTS/releases)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/cmu-sei/GHOSTS/pulls)

[Quick Start](https://cmu-sei.github.io/GHOSTS/quickstart/) • [Documentation](https://cmu-sei.github.io/GHOSTS/) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Demo Video](https://www.youtube.com/watch?v=EkwK-cqwjjA)

</div>

[GHOSTS 9 is in active development — see announcement here](https://github.com/cmu-sei/GHOSTS/discussions/582).

---

## Overview

GHOSTS is an agent orchestration framework that simulates realistic users on all types of computer systems, generating human-like activity across applications, networks, and workflows. Beyond simple automation, it can dynamically reason, chat, and create content via integrated LLMs, enabling adaptive, context-aware behavior. Designed for cyber training, research, and simulation, it produces realistic network traffic, supports complex multi-agent scenarios, and leaves behind realistic artifacts. Its modular architecture allows the addition of new agents, behaviors, and lightweight clients, making it a flexible platform for high-fidelity simulations.

**Watch a quick demo:** [3-minute introduction on YouTube](https://www.youtube.com/watch?v=EkwK-cqwjjA)

### Key Capabilities

- 🌐 **Web browsing** with realistic navigation patterns
- 📝 **Document creation and editing** (Word, Excel, PowerPoint, Notepad)
- 📧 **Email communication** (Outlook, sending/receiving)
- 💬 **Chat and messaging** (Pidgin, social interactions)
- 🖥️ **Terminal commands** and system operations
- 🔄 **File operations** (FTP, SFTP, downloads, uploads)
- 🖱️ **UI interactions** (clicks, mouse movements)
- 🔐 **Remote access** (RDP, SSH)
- 🤖 **AI-powered content generation** (LLM integrations)
- 📊 **Activity monitoring and analytics** via Grafana dashboards

## Quick Start

### Installation

The fastest way to get started is using Docker Compose:

```bash
# Clone the repository
git clone https://github.com/cmu-sei/GHOSTS.git
cd GHOSTS

# Start the GHOSTS API and supporting services
docker-compose up -d
```

For detailed installation instructions, platform-specific builds, and configuration options, see the [Quick Start Guide](https://cmu-sei.github.io/GHOSTS/quickstart/).

### Basic Usage

1. **Deploy the API Server** - Use Docker Compose or deploy to your infrastructure
2. **Install Clients** - Deploy GHOSTS clients on Windows or Linux machines
3. **Configure Timelines** - Define activities through the UI or API
4. **Monitor Activity** - View real-time NPC behavior through Grafana dashboards

See the [full documentation](https://cmu-sei.github.io/GHOSTS/) for detailed configuration and usage examples.

---

## Architecture

GHOSTS consists of several integrated components that work together to create a realistic simulation environment:

### Core Components

| Component | Description | Documentation |
|-----------|-------------|---------------|
| **GHOSTS Client** | Cross-platform agent (Windows/Linux) that executes simulated user activities | [Client Docs](https://cmu-sei.github.io/GHOSTS/core/client/) |
| **GHOSTS API** | Central server managing clients, timelines, and activity orchestration via REST and WebSocket | [API Docs](https://cmu-sei.github.io/GHOSTS/core/api/) |
| **GHOSTS UI** | Web-based interface for managing machines, groups, and deploying timelines | [UI Docs](https://cmu-sei.github.io/GHOSTS/core/ui/) |
| **GHOSTS Lite** | Lightweight client version for resource-constrained environments | [Lite Docs](https://cmu-sei.github.io/GHOSTS/core/lite/) |

### Supporting Services

| Service | Description | Documentation |
|---------|-------------|---------------|
| **Animator** | Generates realistic NPC personas with attributes, relationships, and social networks | [Animator Docs](https://cmu-sei.github.io/GHOSTS/animator/) |
| **Pandora** | Content generation server providing dynamic web content and responses | [Pandora Docs](https://cmu-sei.github.io/GHOSTS/content/pandora/) |
| **Socializer** | Simulated social media platform for realistic social interactions | [Socializer Docs](https://cmu-sei.github.io/GHOSTS/content/social/) |
| **Grafana Integration** | Real-time monitoring and visualization of NPC activities | [Grafana Docs](https://cmu-sei.github.io/GHOSTS/core/grafana/) |

## Use Cases

GHOSTS is designed for various cybersecurity and training scenarios:

- **Cyber Training & Exercises** - Populate training environments with realistic user activity
- **Red Team Operations** - Generate believable background noise during security assessments
- **Blue Team Training** - Create realistic network traffic for detection and analysis practice
- **Research & Development** - Test security tools and detection algorithms with realistic data
- **Cyber Range Development** - Build immersive environments with autonomous NPCs
- **Simulation & Modeling** - Generate realistic network behavior patterns for analysis

## What's New

### Version 8.2 (Current)

- **New UI** - [Web-based interface](src/ghosts.ui) for managing machines, groups, and timelines
- **GHOSTS Lite** - [Lightweight client](src/Ghosts.Client.Lite) for resource-constrained environments
- **LLM Integration** - AI-powered content generation (migrate to [RangerAI](https://github.com/cmu-sei/rangerai) for latest AI features)
- **Bug Fixes** - Resolved GUID issues (#385), client path bugs (#384), and animation cancellation issues
- **Documentation Updates** - Enhanced animation documentation

### Version 8.0 Major Changes

> **⚠️ Breaking Changes**: Version 8.0 introduced breaking changes requiring a fresh installation. No upgrade path from previous versions.

**Key Updates:**
- Merged ANIMATOR and SPECTRE into core platform (both now archived)
- Migrated from MongoDB to PostgreSQL for better performance
- WebSocket support for real-time NPC connectivity
- Simplified Docker Compose deployment
- Reorganized API endpoints
- Enhanced timeline configuration with random delays

<details>
<summary>View Version 8.1 Changes</summary>

- GHOSTS LITE beta release
- API cleanup for machine updates and groups
- Simplified JSON object structures
- Improved machine group management
- Enhanced timeline delivery system

</details>

For complete version history, see the [releases page](https://github.com/cmu-sei/GHOSTS/releases).

## Documentation

Comprehensive documentation is available at [cmu-sei.github.io/GHOSTS](https://cmu-sei.github.io/GHOSTS/)

**Key Documentation Sections:**
- [Installation Guide](https://cmu-sei.github.io/GHOSTS/quickstart/)
- [Client Configuration](https://cmu-sei.github.io/GHOSTS/core/client/)
- [Handler Reference](https://cmu-sei.github.io/GHOSTS/core/handlers/) - Available activities and configurations
- [Timeline Management](https://cmu-sei.github.io/GHOSTS/core/api/timelines/)
- [Animator NPCs](https://cmu-sei.github.io/GHOSTS/animator/)
- [Advanced Features](https://cmu-sei.github.io/GHOSTS/advanced/)

## Contributing

We welcome contributions from the community! Whether it's bug reports, feature requests, documentation improvements, or code contributions, your input helps make GHOSTS better.

### How to Contribute

1. **Report Issues** - Use the [GitHub issue tracker](https://github.com/cmu-sei/GHOSTS/issues) for bugs and feature requests
2. **Submit Pull Requests** - Fork the repository, create a feature branch, and submit a PR
3. **Improve Documentation** - Help enhance guides, examples, and API documentation
4. **Share Use Cases** - Tell us how you're using GHOSTS in your environment

Please ensure your contributions align with our project goals and maintain code quality standards.

## Support

- **Documentation**: [https://cmu-sei.github.io/GHOSTS/](https://cmu-sei.github.io/GHOSTS/)
- **Issues**: [GitHub Issue Tracker](https://github.com/cmu-sei/GHOSTS/issues)
- **Contact**: Email ddupdyke@sei.cmu.edu for questions and support

## Related Projects

- **[RangerAI](https://github.com/cmu-sei/rangerai)** - Advanced AI integration for GHOSTS (successor to Shadows)
- **ANIMATOR** - Now integrated into GHOSTS core (archived)
- **SPECTRE** - Now integrated into GHOSTS core (archived)

## Acknowledgments

GHOSTS is developed by the Software Engineering Institute (SEI) at Carnegie Mellon University and funded by the Department of Defense.

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for full details.

**Distribution Statement**: [DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017-2025 Carnegie Mellon University. All Rights Reserved.

---

<div>

[Website](https://cmu-sei.github.io/GHOSTS/) • [GitHub](https://github.com/cmu-sei/GHOSTS) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Releases](https://github.com/cmu-sei/GHOSTS/releases)

</div>
