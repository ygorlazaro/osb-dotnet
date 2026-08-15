const vscode = require('vscode');

const KEYWORDS = [
  'AND', 'BASE', 'BOOL', 'BOOLEAN', 'BREAK', 'CATCH', 'CLASS', 'CONSTRUCTOR',
  'CONTINUE', 'COUNT', 'DO', 'ELIF', 'ELSE', 'END', 'FALSE', 'FLOOR', 'FOR',
  'FUNCTION', 'GLOBAL', 'IF', 'INPUT', 'INTERFACE', 'KISS', 'ME', 'NEW', 'NOT', 'NULL',
  'NUMBER', 'OR', 'PRINT', 'PRIVATE', 'PROTECTED', 'PUBLIC', 'RETURN', 'SQRT',
  'STEP', 'STR', 'STRING', 'SWITCH', 'CASE', 'DEFAULT', 'THEN', 'TO', 'TRUE',
  'TRY', 'TYPEOF', 'USING', 'VAR', 'VIRTUAL', 'OVERRIDE', 'EVENT', 'ON', 'RAISE',
  'WHILE', 'OBJECT', 'TYPE', 'CLS', 'CLEAR', 'MATH', 'FILE', 'DIR'
];

const BUILTINS = [
  'STR', 'NUMBER', 'BOOL', 'SQRT', 'ABS', 'POW', 'FLOOR', 'CEIL', 'COUNT', 'TYPEOF'
];

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

    return new vscode.CompletionList(items, true);
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

    return items;
  }
}

function activate(context) {
  const provider = new OslangCompletionProvider();
  const selector = { language: 'oslang', scheme: 'file' };

  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(selector, provider, ' ', '.', '(', ')', '[', ']', ',', '=', "'")
  );
}

function deactivate() {}

module.exports = { activate, deactivate };
