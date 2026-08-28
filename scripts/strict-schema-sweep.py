import io, os, re, json

src=[]
for base,dirs,files in os.walk('src'):
    dirs[:]=[d for d in dirs if d not in ('bin','obj')]
    src += [os.path.join(base,f) for f in files if f.endswith('.cs')]
text={p: io.open(p,encoding='utf-8',errors='replace').read() for p in src}

def balanced(s, i, o='(', c=')'):
    d=0
    while i < len(s):
        if s[i]==o: d+=1
        elif s[i]==c:
            d-=1
            if d==0: return i
        i+=1
    return len(s)

# ---- records
records={}
for p,s in text.items():
    for m in re.finditer(r'record\s+(\w+)\s*\(', s):
        end=balanced(s, m.end()-1)
        body=s[m.end():end]
        mem=[]
        for pm in re.finditer(r'JsonPropertyName\("([^"]+)"\)\s*\]\s*(?:\[[^\]]*\]\s*)*([^\s,][^,]*?)\s+(\w+)\s*(?=,|$)', body):
            mem.append({'json':pm.group(1),'type':pm.group(2).strip(),'clr':pm.group(3)})
        if mem: records[m.group(1)]={'file':p,'members':mem}

# ---- named static dictionaries of JsonValueKind
named={}
for p,s in text.items():
    for m in re.finditer(r'Dictionary<string,\s*JsonValueKind\[\]>\s+(\w+)\s*=\s*new\(\)?\s*\{', s):
        end=balanced(s, s.index('{', m.end()-1), '{','}')
        blob=s[m.end()-1:end+1]
        named[m.group(1)]={k:[x.strip().replace('JsonValueKind.','') for x in v.split(',') if x.strip()]
                           for k,v in re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]', blob)}

sites=[]
for p,s in text.items():
    for m in re.finditer(r'ReadStrictJsonAsync<(\w+)>\s*\(', s):
        typ=m.group(1)
        if typ=='T': continue                      # the two generic DEFINITIONS, not call sites
        end=balanced(s, m.end()-1)
        args=s[m.end():end]
        kinds={k:[x.strip().replace('JsonValueKind.','') for x in v.split(',') if x.strip()]
               for k,v in re.findall(r'\["([^"]+)"\]\s*=\s*\[([^\]]*)\]', args)}
        alias=None
        if not kinds:
            for nm in named:
                if re.search(r'\b'+nm+r'\b', args): kinds=named[nm]; alias=nm; break
        rf=re.search(r'requiredFields\s*:\s*\[([^\]]*)\]', args)
        sites.append({'file':p.replace(chr(92),'/'),'line':s[:m.start()].count('\n')+1,'type':typ,
                      'kinds':kinds,'alias':alias,
                      'required':re.findall(r'"([^"]+)"', rf.group(1)) if rf else None})

k1=[];k2=[];k3=[];k4=[];unres=[]
for st in sites:
    rec=records.get(st['type'])
    if rec is None or not st['kinds']:
        unres.append(st); continue
    declared={m['json'] for m in rec['members']}
    keys=set(st['kinds'])
    for miss in sorted(declared-keys): k1.append((st,miss))
    for dead in sorted(keys-declared): k2.append((st,dead))
    for m in rec['members']:
        kinds=st['kinds'].get(m['json'])
        if not kinds: continue
        nullable=m['type'].rstrip().endswith('?')
        if nullable and 'Null' not in kinds: k3.append((st,m['json'],m['type'],kinds,'nullable without Null'))
    for m in rec['members']:
        if st['required'] and m['json'] in st['required'] and m['type'].rstrip().endswith('?'):
            k4.append((st,m['json'],m['type']))

print('CALL SITES (excluding both generic definitions):', len(sites))
print('records with JsonPropertyName members:', len(records))
print('named static dictionaries resolved:', list(named))
print()
for label,coll in [('KIND 1 contract member absent from dictionary (UNREACHABLE)',k1),
                   ('KIND 2 dictionary key with no contract member (DEAD/TYPO)',k2)]:
    print(f'--- {label}: {len(coll)}')
    for st,x in coll: print(f"    {st['file'].split('/')[-1]}:{st['line']} {st['type']} -> {x}")
print(f'--- KIND 3 nullable member without JsonValueKind.Null (explicit null = 400): {len(k3)}')
for st,j,t,k,why in k3: print(f"    {st['file'].split('/')[-1]}:{st['line']} {st['type']}.{j} ({t}) = [{','.join(k)}]")
print(f'--- KIND 4 optional member named in requiredFields: {len(k4)}')
for st,j,t in k4: print(f"    {st['file'].split('/')[-1]}:{st['line']} {st['type']}.{j} ({t})")
print(f'--- UNRESOLVED by this walk (the floor): {len(unres)}')
for st in unres: print(f"    {st['file'].split('/')[-1]}:{st['line']} {st['type']}")
