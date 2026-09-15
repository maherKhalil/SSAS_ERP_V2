import re
import os

sql_file = r'c:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2\scripts\HIS_Migration.sql'
out_dir = r'c:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2\src\Modules\HIS\SSAS.HIS.Domain\Entities'

schemas = ['Radiology', 'Laboratory', 'InPatient', 'OutPatient']

insert_pattern = re.compile(r'INSERT INTO \[(?P<schema>[^\]]+)\]\.\[(?P<table>[^\]]+)\] \((?P<cols>.*?)\)')

def sanitize_name(name):
    # Replace invalid chars for C# identifiers
    name = re.sub(r'[^a-zA-Z0-9_]', '_', name)
    if name[0].isdigit():
        name = '_' + name
    return name

def guess_type(col_name):
    lower_col = col_name.lower()
    if lower_col == 'tenantid': return 'Guid'
    if lower_col == 'id' or lower_col.endswith('id'): return 'int'
    if lower_col.startswith('is') or lower_col.startswith('has'): return 'bool'
    if 'date' in lower_col or 'time' in lower_col: return 'DateTime'
    if 'price' in lower_col or 'amount' in lower_col or 'cost' in lower_col or 'total' in lower_col: return 'decimal'
    return 'string'

with open(sql_file, 'r', encoding='utf-8') as f:
    for line in f:
        m = insert_pattern.search(line)
        if m:
            schema = m.group('schema')
            if schema in schemas:
                table = m.group('table')
                cols_str = m.group('cols')
                
                # Sanitize table name
                class_name = sanitize_name(table)
                
                schema_dir = os.path.join(out_dir, schema)
                os.makedirs(schema_dir, exist_ok=True)
                
                cols = [c.strip('[] ') for c in cols_str.split(',')]
                
                class_path = os.path.join(schema_dir, f"{class_name}.cs")
                with open(class_path, 'w', encoding='utf-8') as cf:
                    cf.write('using System;\n')
                    cf.write('using SSAS.BuildingBlocks.Domain;\n\n')
                    cf.write(f'namespace SSAS.HIS.Domain.Entities.{schema}\n')
                    cf.write('{\n')
                    cf.write(f'    public class {class_name} : ITenantOwnedEntity\n')
                    cf.write('    {\n')
                    
                    seen = set()
                    for col in cols:
                        s_col = sanitize_name(col)
                        if s_col == class_name:
                            s_col = s_col + 'Value'
                        if s_col in seen: continue
                        seen.add(s_col)
                        
                        t = guess_type(s_col)
                        cf.write(f'        public {t} {s_col} {{ get; set; }}\n')
                    cf.write('    }\n')
                    cf.write('}\n')

print("Done parsing.")
