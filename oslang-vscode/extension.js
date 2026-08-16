const vscode = require('vscode');

const KEYWORDS = [
  'AND', 'BASE', 'BOOL', 'BOOLEAN', 'BREAK', 'CATCH', 'CLASS', 'CONSTRUCTOR',
  'CONTINUE', 'COUNT', 'DO', 'ELIF', 'ELSE', 'END', 'FALSE', 'FLOOR', 'FOR',
  'FUNCTION', 'GLOBAL', 'IF', 'INPUT', 'INTERFACE', 'KISS', 'ME', 'NEW', 'NOT', 'NULL',
  'NUMBER', 'OR', 'PRINT', 'PRIVATE', 'PROTECTED', 'PUBLIC', 'RETURN', 'SQRT',
  'STEP', 'STR', 'STRING', 'SWITCH', 'CASE', 'DEFAULT', 'THEN', 'TO', 'TRUE',
  'TRY', 'TYPEOF', 'USING', 'VAR', 'VIRTUAL', 'OVERRIDE', 'EVENT', 'ON', 'RAISE',
  'WHILE', 'OBJECT', 'TYPE', 'CLS', 'CLEAR', 'MATH', 'FILE', 'DIR', 'DATE', 'TIME',
  'SHOW', 'MOD', 'ENUM', 'OSL'
];

const BUILTINS = [
  'STR', 'NUMBER', 'BOOL', 'SQRT', 'ABS', 'POW', 'FLOOR', 'CEIL', 'COUNT', 'TYPEOF',
  'LENGTH', 'TOUPPER', 'TOLOWER', 'TRIM', 'SUBSTR', 'CONTAINS', 'REVERSE', 'NORMALIZE',
  'SORT', 'JOIN', 'FIRST', 'LAST', 'INDEXOF', 'REMOVE', 'FLAT', 'PUSH', 'POP',
  'FINDINDEX', 'REPEAT', 'PADSTART', 'PADEND', 'TRUNC', 'SIN', 'COS', 'TAN', 'PI',
  'RANDOM', 'NOW', 'FORMAT'
];

const MATH_FUNCTIONS = [
  'SQRT', 'POW', 'FLOOR', 'CEIL', 'ABS', 'SIN', 'COS', 'TAN', 'PI', 'RANDOM', 'MAX', 'MIN'
];

const STRING_METHODS = [
  'LENGTH', 'TOUPPER', 'TOLOWER', 'TRIM', 'SUBSTR', 'CONTAINS', 'REVERSE', 'NORMALIZE',
  'REPEAT', 'PADSTART', 'PADEND'
];

const ARRAY_METHODS = [
  'COUNT', 'FIRST', 'LAST', 'SORT', 'JOIN', 'INDEXOF', 'REMOVE', 'REVERSE', 'FLAT',
  'PUSH', 'POP', 'FINDINDEX'
];

const I18N_METHODS = [
  'GET', 'HAS', 'KEYS', 'LANGUAGE', 'SETLANGUAGE', 'LANGUAGES', 'LOAD', 'LOADLANGUAGE',
  'RELOAD', 'UNLOAD', 'DEFAULT', 'SETDEFAULT', 'SETFALLBACK'
];

const FILE_METHODS = [
  'EXISTS', 'READ', 'WRITE', 'APPEND', 'DELETE', 'LIST', 'FILES', 'DIRS', 'CREATE'
];

const DIR_METHODS = [
  'EXISTS', 'LIST', 'FILES', 'DIRS', 'CREATE', 'DELETE', 'CURRENT'
];

const DATE_METHODS = [
  'NOW', 'YEAR', 'MONTH', 'DAY', 'HOUR', 'MINUTE', 'SECOND', 'WEEKDAY', 'FORMAT'
];

function getSignatureHelp(methodName) {
  const signatures = {
    'SQRT': ['MATH.SQRT(x)'],
    'POW': ['MATH.POW(base, exp)'],
    'FLOOR': ['MATH.FLOOR(x)'],
    'CEIL': ['MATH.CEIL(x)'],
    'ABS': ['MATH.ABS(x)'],
    'SIN': ['MATH.SIN(x)'],
    'COS': ['MATH.COS(x)'],
    'TAN': ['MATH.TAN(x)'],
    'RANDOM': ['MATH.RANDOM(max)'],
    'MAX': ['MATH.MAX(a, b)'],
    'MIN': ['MATH.MIN(a, b)'],
    'STR': ['STR(x)'],
    'NUMBER': ['NUMBER(s)'],
    'BOOL': ['BOOL(x)'],
    'TYPEOF': ['TYPEOF(x)'],
    'TRUNC': ['TRUNC(x)', 'TRUNC(x, precision)'],
    'SUBSTR': ['SUBSTR(s, start)', 'SUBSTR(s, start, length)'],
    'PADSTART': ['PADSTART(s, length)', 'PADSTART(s, length, padChar)'],
    'PADEND': ['PADEND(s, length)', 'PADEND(s, length, padChar)'],
    'INDEXOF': ['INDEXOF(arr, value)'],
    'FINDINDEX': ['FINDINDEX(arr, lambda)'],
    'I18N.GET': ['I18N.GET(key)', 'I18N.GET(key, arg1)', 'I18N.GET(key, arg1, arg2)'],
    'I18N.HAS': ['I18N.HAS(key)'],
    'I18N.KEYS': ['I18N.KEYS()'],
    'I18N.LANGUAGE': ['I18N.LANGUAGE()'],
    'I18N.SETLANGUAGE': ['I18N.SETLANGUAGE(lang)'],
    'I18N.LANGUAGES': ['I18N.LANGUAGES()'],
    'FILE.EXISTS': ['FILE.EXISTS(path)'],
    'FILE.READ': ['FILE.READ(path)'],
    'FILE.WRITE': ['FILE.WRITE(path, content)'],
    'FILE.APPEND': ['FILE.APPEND(path, content)'],
    'FILE.DELETE': ['FILE.DELETE(path)'],
    'DIR.EXISTS': ['DIR.EXISTS(path)'],
    'DIR.LIST': ['DIR.LIST(path)'],
    'DIR.FILES': ['DIR.FILES(path)'],
    'DIR.DIRS': ['DIR.DIRS(path)'],
    'DIR.CREATE': ['DIR.CREATE(path)'],
    'DIR.DELETE': ['DIR.DELETE(path)'],
    'DIR.CURRENT': ['DIR.CURRENT()'],
    'DATE.NOW': ['DATE.NOW()'],
    'DATE.FORMAT': ['DATE.FORMAT(date, pattern)']
  };
  const key = methodName.toUpperCase();
  return signatures[key] || [`${methodName}(...)`];
}

class OslangCompletionProvider {
  constructor() {
    this.keywordItems = KEYWORDS.map(k => new vscode.CompletionItem(k, vscode.CompletionItemKind.Keyword));
    this.builtinItems = BUILTINS.map(b => new vscode.CompletionItem(b, vscode.CompletionItemKind.Function));
  }

  provideCompletionItems(document, position, token, context) {
    const items = [];
    const text = document.getText();
    const linePrefix = document.lineAt(position).text.slice(0, position.character);
    const triggerChar = linePrefix[linePrefix.length - 1];

    items.push(...this.keywordItems);
    items.push(...this.builtinItems);

    const entities = this.extractEntities(text);
    items.push(...entities);

    const methodSuggestions = this.getMethodSuggestions(linePrefix);
    items.push(...methodSuggestions);

    return new vscode.CompletionList(items, true);
  }

  getMethodSuggestions(linePrefix) {
    const items = [];
    const upper = linePrefix.toUpperCase();

    if (upper.includes('MATH.')) {
      MATH_FUNCTIONS.forEach(f => {
        items.push(new vscode.CompletionItem(f, vscode.CompletionItemKind.Method, { detail: 'MATH.' + f }));
      });
    }

    if (upper.includes('STRING.') || upper.includes('"') || upper.includes("'")) {
      STRING_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'String method' }));
      });
    }

    if (upper.includes('ARRAY.') || upper.includes('[')) {
      ARRAY_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'Array method' }));
      });
    }

    if (upper.includes('I18N.')) {
      I18N_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'I18N method' }));
      });
    }

    if (upper.includes('FILE.')) {
      FILE_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'FILE method' }));
      });
    }

    if (upper.includes('DIR.')) {
      DIR_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'DIR method' }));
      });
    }

    if (upper.includes('DATE.')) {
      DATE_METHODS.forEach(m => {
        items.push(new vscode.CompletionItem(m, vscode.CompletionItemKind.Method, { detail: 'DATE method' }));
      });
    }

    return items;
  }

  extractEntities(text) {
    const items = [];
    const upper = text.toUpperCase();
    const seen = new Set();

    const addItem = (name, kind) => {
      const key = `${kind}:${name.toUpperCase()}`;
      if (!seen.has(key)) {
        seen.add(key);
        items.push(new vscode.CompletionItem(name, kind));
      }
    };

    const classRegex = /CLASS\s+([A-Z][A-Z0-9_]*)/g;
    let match;
    while ((match = classRegex.exec(upper)) !== null) {
      const name = text.slice(match.index + 6, match.index + 6 + match[1].length);
      addItem(name, vscode.CompletionItemKind.Class);
    }

    const interfaceRegex = /INTERFACE\s+([A-Z][A-Z0-9_]*)/g;
    while ((match = interfaceRegex.exec(upper)) !== null) {
      const name = text.slice(match.index + 10, match.index + 10 + match[1].length);
      addItem(name, vscode.CompletionItemKind.Interface);
    }

    const functionRegex = /FUNCTION\s+([A-Z][A-Z0-9_]*)/g;
    while ((match = functionRegex.exec(upper)) !== null) {
      const name = text.slice(match.index + 9, match.index + 9 + match[1].length);
      addItem(name, vscode.CompletionItemKind.Function);
    }

    const methodRegex = /(?:PUBLIC|PRIVATE|PROTECTED)?\s*([A-Z][A-Z0-9_]+)\s*\(/g;
    while ((match = methodRegex.exec(upper)) !== null) {
      const candidate = match[1].toUpperCase();
      if (!KEYWORDS.includes(candidate) && !BUILTINS.includes(candidate)) {
        const name = text.slice(match.index, match.index + match[1].length);
        addItem(name, vscode.CompletionItemKind.Method);
      }
    }

    const varRegex = /VAR\s+([A-Z][A-Z0-9_]*)/g;
    while ((match = varRegex.exec(upper)) !== null) {
      const name = text.slice(match.index + 4, match.index + 4 + match[1].length);
      addItem(name, vscode.CompletionItemKind.Variable);
    }

    const assignRegex = /([A-Z][A-Z0-9_]+)\s*=/g;
    while ((match = assignRegex.exec(upper)) !== null) {
      const candidate = match[1].toUpperCase();
      if (!KEYWORDS.includes(candidate) && !BUILTINS.includes(candidate)) {
        const name = text.slice(match.index, match.index + match[1].length);
        addItem(name, vscode.CompletionItemKind.Variable);
      }
    }

    const enumRegex = /ENUM\s+([A-Z][A-Z0-9_]*)/g;
    while ((match = enumRegex.exec(upper)) !== null) {
      const name = text.slice(match.index + 5, match.index + 5 + match[1].length);
      addItem(name, vscode.CompletionItemKind.Enum);
    }

    return items;
  }
}

class OslangHoverProvider {
  constructor() {
    this.docs = new Map([
      ['SQRT', 'MATH.SQRT(x) - Returns the square root of x'],
      ['POW', 'MATH.POW(base, exp) - Returns base raised to the power of exp'],
      ['FLOOR', 'MATH.FLOOR(x) - Returns the largest integer less than or equal to x'],
      ['CEIL', 'MATH.CEIL(x) - Returns the smallest integer greater than or equal to x'],
      ['ABS', 'MATH.ABS(x) - Returns the absolute value of x'],
      ['SIN', 'MATH.SIN(x) - Returns the sine of x (radians)'],
      ['COS', 'MATH.COS(x) - Returns the cosine of x (radians)'],
      ['TAN', 'MATH.TAN(x) - Returns the tangent of x (radians)'],
      ['PI', 'MATH.PI - Returns the value of PI (3.14159...)'],
      ['RANDOM', 'MATH.RANDOM(max) - Returns a random integer from 0 to max-1'],
      ['MAX', 'MATH.MAX(a, b) - Returns the larger of two numbers'],
      ['MIN', 'MATH.MIN(a, b) - Returns the smaller of two numbers'],
      ['STR', 'STR(x) - Converts x to a string'],
      ['NUMBER', 'NUMBER(s) - Converts s to a number'],
      ['BOOL', 'BOOL(x) - Converts x to a boolean'],
      ['TYPEOF', 'TYPEOF(x) - Returns the type name of x as a string'],
      ['TRUNC', 'TRUNC(x) or TRUNC(x, precision) - Truncates a number'],
      ['SUBSTR', 'SUBSTR(s, start) or SUBSTR(s, start, length) - Returns a substring'],
      ['PADSTART', 'PADSTART(s, length) or PADSTART(s, length, padChar) - Pads string on the left'],
      ['PADEND', 'PADEND(s, length) or PADEND(s, length, padChar) - Pads string on the right'],
      ['LENGTH', 'LENGTH(s) - Returns the length of a string'],
      ['TOUPPER', 'TOUPPER(s) - Returns the string in uppercase'],
      ['TOLOWER', 'TOLOWER(s) - Returns the string in lowercase'],
      ['TRIM', 'TRIM(s) - Removes leading/trailing whitespace'],
      ['CONTAINS', 'CONTAINS(s, substr) - Returns TRUE if s contains substr'],
      ['REVERSE', 'REVERSE(s) or REVERSE(arr) - Reverses string or array in place'],
      ['NORMALIZE', 'NORMALIZE(s) - Removes diacritics from string'],
      ['REPEAT', 'REPEAT(s, n) - Returns s repeated n times'],
      ['INDEXOF', 'INDEXOF(arr, value) - Returns index of first occurrence or -1'],
      ['FINDINDEX', 'FINDINDEX(arr, lambda) - Returns index of first element matching predicate'],
      ['COUNT', 'COUNT(x) - Returns number of elements in array or string'],
      ['FIRST', 'FIRST(arr) - Returns the first element of an array'],
      ['LAST', 'LAST(arr) - Returns the last element of an array'],
      ['SORT', 'SORT(arr) - Returns a new sorted array'],
      ['JOIN', 'JOIN(arr, sep) - Returns elements joined by separator'],
      ['PUSH', 'PUSH(arr, value) - Adds value to the end of the array'],
      ['POP', 'POP(arr) - Removes and returns the last element'],
      ['FLAT', 'FLAT(arr) - Returns a flattened copy of the array'],
      ['REMOVE', 'REMOVE(arr, value) - Removes first occurrence of value'],
      ['I18N.GET', 'I18N.GET(key, ...) - Returns translated string for key'],
      ['I18N.HAS', 'I18N.HAS(key) - Returns TRUE if key exists'],
      ['I18N.KEYS', 'I18N.KEYS() - Returns all translation keys'],
      ['I18N.LANGUAGE', 'I18N.LANGUAGE() - Returns current language code'],
      ['I18N.SETLANGUAGE', 'I18N.SETLANGUAGE(lang) - Sets the active language'],
      ['I18N.LANGUAGES', 'I18N.LANGUAGES() - Returns available language codes'],
      ['FILE.EXISTS', 'FILE.EXISTS(path) - Checks if file exists'],
      ['FILE.READ', 'FILE.READ(path) - Reads entire file as string'],
      ['FILE.WRITE', 'FILE.WRITE(path, content) - Writes content to file'],
      ['FILE.APPEND', 'FILE.APPEND(path, content) - Appends content to file'],
      ['FILE.DELETE', 'FILE.DELETE(path) - Deletes a file'],
      ['DIR.EXISTS', 'DIR.EXISTS(path) - Checks if directory exists'],
      ['DIR.LIST', 'DIR.LIST(path) - Lists files and directories'],
      ['DIR.FILES', 'DIR.FILES(path) - Lists only files'],
      ['DIR.DIRS', 'DIR.DIRS(path) - Lists only directories'],
      ['DIR.CREATE', 'DIR.CREATE(path) - Creates a directory'],
      ['DIR.DELETE', 'DIR.DELETE(path) - Deletes a directory'],
      ['DIR.CURRENT', 'DIR.CURRENT() - Returns the current directory path'],
      ['DATE.NOW', 'DATE.NOW() - Returns the current date and time'],
      ['DATE.FORMAT', 'DATE.FORMAT(date, pattern) - Formats a date'],
      ['PRINT', 'PRINT expr1, expr2, ... - Prints expressions with newline'],
      ['SHOW', 'SHOW expr1, expr2, ... - Prints expressions without newline'],
      ['INPUT', 'INPUT prompt, var - Reads user input into var'],
      ['CLEAR', 'CLEAR - Clears the screen'],
      ['CLS', 'CLS - Clears the screen (alias)'],
      ['IF', 'IF condition THEN ... END - Conditional execution'],
      ['FOR', 'FOR var = start TO end [STEP s] ... END - Loop'],
      ['WHILE', 'WHILE condition ... END - Loop while condition is true'],
      ['DO', 'DO ... WHILE condition - Loop that executes at least once'],
      ['SWITCH', 'SWITCH expr CASE val ... DEFAULT ... END - Multi-way branch'],
      ['TRY', 'TRY ... CATCH err ... END - Catches runtime errors'],
      ['RAISE', 'RAISE message - Throws a runtime error'],
      ['FUNCTION', 'FUNCTION name(params) ... END FUNCTION - Defines a function'],
      ['CLASS', 'CLASS Name ... END CLASS - Defines a class'],
      ['INTERFACE', 'INTERFACE Name ... END INTERFACE - Defines an interface'],
      ['ENUM', 'ENUM Name member1 [= val1] ... END - Defines an enumeration'],
      ['VAR', 'VAR name TYPE - Declares a variable with type'],
      ['GLOBAL', 'GLOBAL name = value - Declares a global variable'],
      ['USING', 'USING Namespace - Imports a namespace'],
      ['EVENT', 'EVENT Name - Declares an event'],
      ['ON', 'ON Event = handler - Registers an event handler'],
      ['NEW', 'NEW ClassName(args) - Creates a new instance'],
      ['BASE', 'BASE(args) - Calls the parent constructor'],
      ['ME', 'ME - Refers to the current instance inside a class']
    ]);
  }

  provideHover(document, position, token) {
    const range = document.getWordRangeAtPosition(position, /[A-Z_][A-Z0-9_]*/);
    if (!range) return null;

    const word = document.getText(range).toUpperCase();
    if (this.docs.has(word)) {
      return new vscode.Hover(new vscode.MarkdownString(this.docs.get(word)));
    }
    return null;
  }
}

class OslangDocumentSymbolProvider {
  provideDocumentSymbols(document, token) {
    const text = document.getText();
    const symbols = [];
    const lines = text.split('\n');

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const upper = line.toUpperCase();

      const classMatch = upper.match(/^\s*CLASS\s+([A-Z][A-Z0-9_]*)/);
      if (classMatch) {
        const name = line.slice(line.toUpperCase().indexOf('CLASS') + 5).trim().split(/\s+/)[0];
        const startLine = i;
        const endLine = this.findEnd(lines, i, 'END');
        const sym = new vscode.DocumentSymbol(name, 'class', vscode.SymbolKind.Class,
          new vscode.Position(startLine, 0),
          new vscode.Position(endLine, lines[endLine].length));
        sym.children = this.extractMembers(lines, startLine + 1, endLine);
        symbols.push(sym);
        i = endLine;
        continue;
      }

      const interfaceMatch = upper.match(/^\s*INTERFACE\s+([A-Z][A-Z0-9_]*)/);
      if (interfaceMatch) {
        const name = line.slice(line.toUpperCase().indexOf('INTERFACE') + 9).trim().split(/\s+/)[0];
        const startLine = i;
        const endLine = this.findEnd(lines, i, 'END');
        const sym = new vscode.DocumentSymbol(name, 'interface', vscode.SymbolKind.Interface,
          new vscode.Position(startLine, 0),
          new vscode.Position(endLine, lines[endLine].length));
        sym.children = this.extractMembers(lines, startLine + 1, endLine);
        symbols.push(sym);
        i = endLine;
        continue;
      }

      const enumMatch = upper.match(/^\s*ENUM\s+([A-Z][A-Z0-9_]*)/);
      if (enumMatch) {
        const name = line.slice(line.toUpperCase().indexOf('ENUM') + 4).trim().split(/\s+/)[0];
        const startLine = i;
        const endLine = this.findEnd(lines, i, 'END');
        const sym = new vscode.DocumentSymbol(name, 'enum', vscode.SymbolKind.Enum,
          new vscode.Position(startLine, 0),
          new vscode.Position(endLine, lines[endLine].length));
        symbols.push(sym);
        i = endLine;
        continue;
      }

      const functionMatch = upper.match(/^\s*FUNCTION\s+([A-Z][A-Z0-9_]*)/);
      if (functionMatch) {
        const name = line.slice(line.toUpperCase().indexOf('FUNCTION') + 8).trim().split(/[(\s]+/)[0];
        const startLine = i;
        const endLine = this.findEnd(lines, i, 'END');
        symbols.push(new vscode.DocumentSymbol(name, 'function', vscode.SymbolKind.Function,
          new vscode.Position(startLine, 0),
          new vscode.Position(endLine, lines[endLine].length)));
        i = endLine;
        continue;
      }
    }

    return symbols;
  }

  findEnd(lines, start, keyword) {
    const upperKeyword = keyword.toUpperCase();
    for (let i = start + 1; i < lines.length; i++) {
      if (lines[i].toUpperCase().trim().startsWith(upperKeyword)) {
        return i;
      }
    }
    return Math.min(start + 1, lines.length - 1);
  }

  extractMembers(lines, start, end) {
    const members = [];
    for (let i = start; i < end; i++) {
      const line = lines[i];
      const upper = line.toUpperCase();

      const methodMatch = upper.match(/^\s*(?:PUBLIC|PRIVATE|PROTECTED|VIRTUAL|OVERRIDE)?\s*(FUNCTION|CONSTRUCTOR)\s+([A-Z][A-Z0-9_]*)/);
      if (methodMatch) {
        const name = methodMatch[2];
        members.push(new vscode.DocumentSymbol(name, 'method', vscode.SymbolKind.Method,
          new vscode.Position(i, 0),
          new vscode.Position(i, line.length)));
      }

      const propMatch = upper.match(/^\s*(?:PUBLIC|PRIVATE|PROTECTED)\s+VAR\s+([A-Z][A-Z0-9_]*)/);
      if (propMatch) {
        const name = propMatch[1];
        members.push(new vscode.DocumentSymbol(name, 'property', vscode.SymbolKind.Property,
          new vscode.Position(i, 0),
          new vscode.Position(i, line.length)));
      }
    }
    return members;
  }
}

class OslangFoldingProvider {
  provideFoldingRanges(document, token, context) {
    const ranges = [];
    const lines = document.getText().split('\n');

    for (let i = 0; i < lines.length; i++) {
      const upper = lines[i].toUpperCase();
      const startLine = i;

      if (upper.match(/^\s*(CLASS|INTERFACE|FUNCTION|CONSTRUCTOR|TRY|IF|FOR|WHILE|DO|SWITCH|ENUM)/)) {
        const endLine = this.findBlockEnd(lines, i);
        if (endLine > startLine + 1) {
          ranges.push(new vscode.FoldingRange(startLine, endLine, vscode.FoldingRangeKind.Region));
        }
        i = endLine;
      }
    }

    return ranges;
  }

  findBlockEnd(lines, start) {
    const upperStart = lines[start].toUpperCase();
    const isFunction = upperStart.includes('FUNCTION');
    const isConstructor = upperStart.includes('CONSTRUCTOR');

    for (let i = start + 1; i < lines.length; i++) {
      const upper = lines[i].toUpperCase().trim();
      if (isFunction && upper === 'END FUNCTION') return i;
      if (isConstructor && upper === 'END') return i;
      if (upper === 'END') return i;
    }
    return lines.length - 1;
  }
}

function activate(context) {
  const completionProvider = new OslangCompletionProvider();
  const hoverProvider = new OslangHoverProvider();
  const signatureProvider = new OslangSignatureProvider();
  const symbolProvider = new OslangDocumentSymbolProvider();
  const foldingProvider = new OslangFoldingProvider();
  const selector = { language: 'oslang', scheme: 'file' };

  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(selector, completionProvider, ' ', '.', '(', ')', '[', ']', ',', '=', "'")
  );

  context.subscriptions.push(
    vscode.languages.registerHoverProvider(selector, hoverProvider)
  );

  context.subscriptions.push(
    vscode.languages.registerSignatureHelpProvider(selector, signatureProvider, '(', ',')
  );

  context.subscriptions.push(
    vscode.languages.registerDocumentSymbolProvider(selector, symbolProvider)
  );

  context.subscriptions.push(
    vscode.languages.registerFoldingRangeProvider(selector, foldingProvider)
  );
}

function deactivate() {}

module.exports = { activate, deactivate };
