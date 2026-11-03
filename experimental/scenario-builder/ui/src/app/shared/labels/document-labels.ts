import { Audience, TemplateFormat } from '../../core/models/builder.model';

export const AUDIENCES: Record<Audience, string> = { participants: 'Participants', facilitators: 'Facilitators', designers: 'Designers' };

export const FORMATS: Record<TemplateFormat, string> = { html: 'HTML', markdown: 'Markdown' };
