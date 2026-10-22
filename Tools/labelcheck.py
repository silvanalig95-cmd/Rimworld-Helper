import glob, re, xml.etree.ElementTree as ET
bad=[]
for f in glob.glob('Defs/**/*.xml', recursive=True):
    try: root=ET.parse(f).getroot()
    except Exception as e: print("PARSE",f,e); continue
    for d in root:
        lab=d.findtext('label')
        if lab and re.search(r'[\[\]{}]',lab) and (d.findtext('ignoreIllegalLabelCharacterConfigError') or '').strip().lower()!='true':
            bad.append((f,d.findtext('defName'),lab))
print(len(bad),"defs need the 1.7 label flag"); [print(" ",*b) for b in bad[:20]]
