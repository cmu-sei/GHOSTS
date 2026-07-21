import { Pipe, PipeTransform } from '@angular/core';

const COUNTRY_FLAGS: Record<string, string> = {
  'United States': '\u{1F1FA}\u{1F1F8}',
  'China': '\u{1F1E8}\u{1F1F3}',
  'Russia': '\u{1F1F7}\u{1F1FA}',
  'India': '\u{1F1EE}\u{1F1F3}',
  'United Kingdom': '\u{1F1EC}\u{1F1E7}',
  'France': '\u{1F1EB}\u{1F1F7}',
  'Germany': '\u{1F1E9}\u{1F1EA}',
  'Japan': '\u{1F1EF}\u{1F1F5}',
  'Brazil': '\u{1F1E7}\u{1F1F7}',
  'Canada': '\u{1F1E8}\u{1F1E6}',
  'Australia': '\u{1F1E6}\u{1F1FA}',
  'South Korea': '\u{1F1F0}\u{1F1F7}',
  'Italy': '\u{1F1EE}\u{1F1F9}',
  'Mexico': '\u{1F1F2}\u{1F1FD}',
  'Indonesia': '\u{1F1EE}\u{1F1E9}',
  'Saudi Arabia': '\u{1F1F8}\u{1F1E6}',
  'Turkey': '\u{1F1F9}\u{1F1F7}',
  'Argentina': '\u{1F1E6}\u{1F1F7}',
  'South Africa': '\u{1F1FF}\u{1F1E6}',
  'European Union': '\u{1F1EA}\u{1F1FA}',
  'North Korea': '\u{1F1F0}\u{1F1F5}',
  'Iran': '\u{1F1EE}\u{1F1F7}',
  'Iraq': '\u{1F1EE}\u{1F1F6}',
  'Syria': '\u{1F1F8}\u{1F1FE}',
  'Taiwan': '\u{1F1F9}\u{1F1FC}',
  'Cuba': '\u{1F1E8}\u{1F1FA}',
  'Ukraine': '\u{1F1FA}\u{1F1E6}',
  'Israel': '\u{1F1EE}\u{1F1F1}',
};

@Pipe({
  name: 'countryFlag',
  standalone: true,
})
export class CountryFlagPipe implements PipeTransform {
  transform(country: string): string {
    return COUNTRY_FLAGS[country] ?? '\u{1F3F3}\u{FE0F}';
  }
}
