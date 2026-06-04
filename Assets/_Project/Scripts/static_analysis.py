#!/usr/bin/env python3
"""
静态分析 GanglandUndercover 项目中的 C# 编译错误。
检查：
1. 方法调用参数数量是否匹配定义
2. 类型引用是否存在
3. 命名空间是否正确
4. 语法错误（括号不匹配、分号缺失等）
"""

import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent.parent  # 向上4级到项目根目录
SCRIPTS_DIR = SCRIPT_DIR

errors = []

def check_method_calls(file_path: Path):
    """检查方法调用参数数量是否匹配定义"""
    content = file_path.read_text(encoding='utf-8', errors='ignore')
    lines = content.split('\n')
    
    # 提取所有方法定义（简单正则，不完美但够用）
    method_defs = {}
    for i, line in enumerate(lines):
        # 匹配 public/private/static 方法定义
        m = re.match(r'^\s+(public|private|internal|protected)\s+(static\s+)?[\w<>]+\s+(\w+)\s*\(', line)
        if m:
            method_name = m.group(3)
            # 提取参数列表
            params = re.search(r'\(([^)]*)\)', line)
            if params:
                param_str = params.group(1).strip()
                if param_str:
                    # 计算参数数量（简单计算逗号）
                    param_count = param_str.count(',') + 1
                else:
                    param_count = 0
                method_defs[method_name] = {
                    'param_count': param_count,
                    'line': i + 1,
                    'param_str': param_str
                }
    
    # 检查调用（跨文件，这里只做简单检查）
    # 检查 PlaceRailingSegment 调用
    for i, line in enumerate(lines):
        if 'PlaceRailingSegment(' in line and 'private static' not in line:
            # 计算实际参数数量
            # 简化处理：找到左括号到右括号的内容
            open_paren = line.find('PlaceRailingSegment(')
            if open_paren == -1:
                continue
            # 找到匹配的右括号
            paren_count = 0
            end_pos = open_paren + len('PlaceRailingSegment(')
            arg_str = ''
            for j in range(open_paren + len('PlaceRailingSegment('), len(line)):
                c = line[j]
                if c == '(':
                    paren_count += 1
                elif c == ')':
                    if paren_count == 0:
                        arg_str = line[open_paren + len('PlaceRailingSegment('):j]
                        break
                    paren_count -= 1
            
            # 计算参数数量
            if arg_str:
                # 移除注释
                arg_str = re.sub(r'//.*$', '', arg_str)
                arg_count = arg_str.count(',') + 1 if arg_str.strip() else 0
                if arg_count < 4:
                    errors.append({
                        'file': str(file_path),
                        'line': i + 1,
                        'type': 'CALL_ARG_MISMATCH',
                        'message': f'PlaceRailingSegment 调用参数不足：需要4个参数，实际约{arg_count}个。调用代码：{line.strip()[:80]}'
                    })

def check_syntax_errors(file_path: Path):
    """检查常见语法错误"""
    content = file_path.read_text(encoding='utf-8', errors='ignore')
    lines = content.split('\n')
    
    # 检查括号不匹配
    for i, line in enumerate(lines):
        # 跳过注释行
        if line.strip().startswith('//'):
            continue
            
        # 检查 Color 构造函数参数数量
        color_matches = re.finditer(r'new Color\(', line)
        for match in color_matches:
            start = match.start()
            # 找到匹配的右括号
            paren_count = 0
            end_pos = -1
            for j in range(start + len('new Color('), len(line)):
                c = line[j]
                if c == '(':
                    paren_count += 1
                elif c == ')':
                    if paren_count == 0:
                        end_pos = j
                        break
                    paren_count -= 1
            
            if end_pos != -1:
                args_str = line[start + len('new Color('):end_pos]
                # 计算参数数量
                args = [a.strip() for a in args_str.split(',') if a.strip()]
                if len(args) not in [3, 4]:
                    errors.append({
                        'file': str(file_path),
                        'line': i + 1,
                        'type': 'SYNTAX_ERROR',
                        'message': f'Color 构造函数参数数量错误：{len(args)}个参数（应为3或4）。代码：{line.strip()[:80]}'
                    })
        
        # 检查是否有明显的括号不匹配（行末有多余的 )）
        stripped = line.strip()
        if stripped.endswith(')))') or stripped.endswith('))))'):
            # 可能是括号不匹配
            open_count = stripped.count('(')
            close_count = stripped.count(')')
            if close_count > open_count:
                errors.append({
                    'file': str(file_path),
                    'line': i + 1,
                    'type': 'SYNTAX_ERROR',
                    'message': f'可能的括号不匹配：行末有多余的右括号。代码：{stripped[:80]}'
                })

print("开始静态分析...")
for cs_file in SCRIPTS_DIR.rglob('*.cs'):
    check_method_calls(cs_file)
    check_syntax_errors(cs_file)

if errors:
    print(f"\n发现 {len(errors)} 个潜在问题：\n")
    for err in errors:
        print(f"[{err['type']}] {err['file']}:{err['line']}")
        print(f"  {err['message']}\n")
else:
    print("\n未发现明显的静态分析问题。")

# 特别检查 StreetFurniture.cs 中的 PlaceRailingAlong 和 PlaceRailingSegment
print("\n--- 特别检查 StreetFurniture.cs ---")
sf_file = SCRIPTS_DIR / 'SocialDeduction' / 'StreetFurniture.cs'
if sf_file.exists():
    content = sf_file.read_text(encoding='utf-8', errors='ignore')
    lines = content.split('\n')
    
    # 找到 PlaceRailingAlong 方法
    in_method = False
    method_lines = []
    for i, line in enumerate(lines):
        if 'public static void PlaceRailingAlong(' in line:
            in_method = True
        if in_method:
            method_lines.append((i + 1, line))
            if line.strip() == '}' and 'public static void PlaceRailingAlong(' not in line:
                # 检查方法体
                break
    
    print("PlaceRailingAlong 方法内容：")
    for lineno, line in method_lines:
        print(f"  {lineno}: {line}")
    
    # 检查 PlaceRailingSegment 调用
    print("\nPlaceRailingSegment 调用：")
    for i, line in enumerate(lines):
        if 'PlaceRailingSegment(' in line and 'private static' not in line and 'public static' not in line:
            print(f"  {i+1}: {line.strip()}")
