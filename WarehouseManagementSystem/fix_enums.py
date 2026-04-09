import os

def fix_enum_file(filepath):
    if not os.path.exists(filepath): return
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    new_lines = []
    for line in lines:
        cleaned = line.strip()
        if not cleaned:
            new_lines.append(line)
            continue
        
        if cleaned.startswith('/ ') or cleaned.startswith('/<') or cleaned.startswith('/ <'):
            new_lines.append(line.replace('/', '///', 1))
            continue
            
        if '{' in cleaned or '}' in cleaned or cleaned.startswith('public enum') or cleaned.startswith('enum ') or cleaned.startswith('namespace ') or cleaned.startswith('using ') or cleaned.startswith('['):
            new_lines.append(line)
            continue
            
        if '=' in cleaned:
            parts = line.split(',', 1)
            if len(parts) == 2:
                left = parts[0]
                right = parts[1]
                if right.strip() and not right.strip().startswith('//'):
                    new_lines.append(left + ', //' + right)
                else:
                    new_lines.append(line)
            else:
                if not cleaned.endswith(','): 
                    if not cleaned.startswith('//'):
                        new_lines.append('// ' + line)
                    else:
                        new_lines.append(line)
                else:
                    new_lines.append(line)
        else:
            if not cleaned.startswith('//'):
                white_idx = len(line) - len(line.lstrip())
                new_lines.append(line[:white_idx] + '// ' + line[white_idx:])
            else:
                new_lines.append(line)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

fix_enum_file(r'Shared\Ndc\AciEnum.cs')
fix_enum_file(r'Shared\Ndc\AciHostEventTypeEnum.cs')
fix_enum_file(r'Shared\Ndc\TaskEnums.cs')
