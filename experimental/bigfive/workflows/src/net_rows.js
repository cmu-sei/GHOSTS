// The __TABLE__ rows of the finished run, one item per row.
const rows = $('Finish run').first().json['__TABLE__'];
return (Array.isArray(rows) ? rows : [rows]).map((json) => ({ json }));
