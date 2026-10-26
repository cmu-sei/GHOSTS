// Import Human Data: hand the uploaded spreadsheets to the next Extract node (Extract From File outputs
// rows without the binary, so each sheet extraction starts again from the form submission).
return [{ json: {}, binary: $('Import form').first().binary }];
