from sys import argv
import os
import re


INCLUDE_DIRECTIVE = '#include'
CURRENT_PATH = os.getcwd()

PROCESSED_STACK = []

ST_NORMAL = 0
ST_ACCUMULATE_DICT = 1
ST_REPLACE = 2

def print_help():
    _, script_name = os.path.split(argv[0])
    help_str = 'Extended #include directive file processor.\n\n\
Works both with relative and absolute paths:\n\
\t#include "test.txt"\n\
\t#include "../previous.ext"\n\
\t#include "X:/path/to/file.inc"\n\n\
Common include directive syntax:\n\n\
\t#include "<file_path>" [{]\n\n\
If "{" occures at the end of line, #include directive must be followed by\n\
replace-dictionary declaration:\n\n\
\t#include "<file_path>" {\n\
\t\t\'<some_key>\': \'<some_value>\',\n\
\t\t...\n\
\t}\n\
\t...\n\n\
Replace operation is performed after all #include-s processed, so\n\
each <some_key> appearance in <file_path> and it\'s included files content\n\
will be replaced in output with <some_value> text.\n\n\
Usage format:\n\n\
\tpython ' + script_name + '  <input_file_name>  <output_file_name> [-?]\n\n\
Use -? to see this help.'
    print(help_str)

def process_file(filepath):
    try:
        PROCESSED_STACK.append(filepath)
        file = open(filepath, 'r')
        input_lines = file.readlines()
        file.close()
        status = ST_NORMAL
        result = []
        i = 0
        while i < len(input_lines):
            line = input_lines[i]
            if status == ST_NORMAL:
                if line.strip().startswith(INCLUDE_DIRECTIVE):
                    include_info = parse_include(line)
                    include_path = include_info['path']
                    if not os.path.isfile(include_path):
                        current_dir, _ = os.path.split(filepath)
                        include_path = os.path.join(current_dir, include_path)
                        include_path = os.path.normpath(include_path)
                    if not include_path in PROCESSED_STACK:
                        include_info['lines'] = process_file(include_path)
                        PROCESSED_STACK.remove(include_path)
                    if include_info['replace_dict'] != None:
                        status = ST_ACCUMULATE_DICT
                        i += 1
                    else:
                        status = ST_REPLACE
                else:
                    result.append(line)
                    i += 1
            elif status == ST_ACCUMULATE_DICT:
                if line.strip() == '}':
                    status = ST_REPLACE
                else:
                    key, value = parse_dict_record(line)
                    include_info['replace_dict'][key] = value
                    i += 1
            elif status == ST_REPLACE:
                if include_info['replace_dict']:
                    for key, value in include_info['replace_dict'].items():
                        for j in range(len(include_info['lines'])):
                            line = include_info['lines'][j]
                            include_info['lines'][j] = line.replace(key, value)
                for line in include_info['lines']:
                    result.append(line)
                include_info.clear()
                status = ST_NORMAL
                i += 1
        if status != ST_NORMAL:
            raise EOFError('Unexpected end of file')
    except Exception as e:
        print("Error processing file \"{}\"\n{}".format(filepath, e))
        raise
    if (len(result) > 0) and result[len(result)-1] != '\n':
        result.append('\n')
    return result

def parse_include(line):
    line = line.strip()
    try:
        match = re.match(r'^'+INCLUDE_DIRECTIVE+r'\s*"(.*)"\s*({)?\s*$', line)
        if match:
            path, has_dict = match.group(1, 2)
            result = {
                'path': get_path(path),
                'lines': [],
                'replace_dict': None
            }
            if has_dict:
                result['replace_dict'] = {}
        else:
            raise Exception
    except:
        ValueError('Incorrect include directive: {}'.format(line))
    return result

def get_path(line):
    result = line.replace('/', os.sep)
    return result

def parse_dict_record(line):
    line = line.strip()
    match = re.match(r"^\s*'(.*)'\s*:\s*'(.*)',?$", line)
    if match:
        key, value = match.group(1, 2)
        if key.count("'") != key.count("\\'"):
            raise ValueError('Invalid key in dictionary record: {}'.format(line))
        key = key.replace("\'", "'")
    else:
        raise ValueError('Invalid dictionary record: {}'.format(line))
    return key, value

def write_result(output_filename, output):
    file = open(output_filename, 'w')
    file.writelines(output)
    file.close()

def process():
    input_path = argv[1]
    output_path = argv[2]
    try:
        if not os.path.isfile(input_path):
            input_path = os.path.join(CURRENT_PATH, input_path)
        result = process_file(input_path)
    except FileNotFoundError:
        print("Can't open input file")
    except:
        print("Can't process input file")
    else:
        try:
            output_path = get_path(output_path)
            if not os.path.isfile(output_path):
                output_path = os.path.normpath(os.path.join(CURRENT_PATH, output_path))
            write_result(output_path, result)
        except:
            print("Can't write output file")


if __name__ == "__main__":
    if len(argv) == 1 or '-?' in argv:
        print_help()
    elif len(argv) != 3:
        print("Bad arguments count. Expected 3, got {}\n".format(len(argv)))
        print_help()
    else:
        process()
