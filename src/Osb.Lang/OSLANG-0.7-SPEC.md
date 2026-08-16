# **OSLANG 0.7 Specification**  
## **Terminal & Interactive Applications**  
**Base:** OSLANG 0.62  
 **Version type:** Additive / non-breaking  
 **Primary integration target:** KISS.NET  
 **Extension:** .osl  
# **1. Objetivo**  
A OSLANG 0.7 adiciona os recursos necessários para que aplicações completas e interativas de terminal possam ser escritas em OSLANG.  
O principal critério de aceitação desta versão é:  
O KISS.NET deve poder ser reimplementado em OSLANG utilizando somente a linguagem e APIs genéricas da plataforma OSL, sem funções específicas do KISS no runtime.  
A versão 0.7 deve permitir:  
- aplicações full-screen;  
- entrada de teclado por evento;  
- teclas especiais;  
- posicionamento de cursor;  
- controle de cores;  
- renderização eficiente;  
- detecção de resize;  
- modo de terminal interativo;  
- alternate screen;  
- edição de arquivos de texto;  
- argumentos de linha de comando;  
- tratamento de erros;  
- aplicações com loop de eventos.  
# **2. Filosofia**  
A OSLANG não deve tentar reproduzir a implementação histórica do KISS.  
O objetivo é reproduzir **comportamento**, não APIs antigas.  
Portanto, o KISS não deve depender de:  
PEEK  
POKE  
BIOS  
DOS video memory  
ANSI escape sequences  
Windows Console API  
Unix ioctl  
scan codes específicos da plataforma  
Essas diferenças ficam dentro do runtime .NET.  
O programa OSLANG deve enxergar uma API abstrata.  
# **3. Novos namespaces**  
A 0.7 adiciona:  
USING OSL.CONSOLE  
USING OSL.APP  
O namespace de arquivos existente continua sendo utilizado:  
USING OSL.FILE  
# **4. Arquitetura**  
A divisão conceitual passa a ser:  
OSLANG Application  
       │  
       ├── OSL.APP  
       │  
       ├── OSL.CONSOLE  
       │  
       ├── OSL.FILE  
       │  
       └── OSL language  
              │  
              ├── Classes  
              ├── Arrays  
              ├── Strings  
              ├── Enums  
              ├── Functions  
              └── Control flow  
O runtime:  
OSLANG  
   ↓  
Extension Registry  
   ↓  
.NET Host  
   ↓  
Terminal / Filesystem  
# **5. OSL.CONSOLE**  
OSL.CONSOLE é a API oficial para aplicações de terminal.  
Ela fornece:  
terminal dimensions  
cursor  
screen  
keyboard  
colors  
terminal mode  
alternate screen  
rendering  
resize  
bell  
# **6. Dimensões do terminal**  
## **WIDTH**  
Width = OSL.CONSOLE.WIDTH()  
Retorna o número atual de colunas.  
## **HEIGHT**  
Height = OSL.CONSOLE.HEIGHT()  
Retorna o número atual de linhas.  
## **SIZE**  
Size = OSL.CONSOLE.SIZE()  
Retorna um objeto contendo:  
WIDTH  
HEIGHT  
Exemplo:  
Size = OSL.CONSOLE.SIZE()  
   
PRINT Size.WIDTH  
PRINT Size.HEIGHT  
As coordenadas do terminal são **1-based**.  
# **7. Resize**  
## **RESIZED**  
IF OSL.CONSOLE.RESIZED() THEN  
    RENDER()  
END  
Retorna TRUE quando o tamanho do terminal mudou desde a última verificação.  
# **8. Cursor**  
## **SETCURSOR**  
OSL.CONSOLE.SETCURSOR(Row, Column)  
Move o cursor.  
Exemplo:  
OSL.CONSOLE.SETCURSOR(10, 20)  
SHOW "Hello"  
## **GETCURSOR**  
Position = OSL.CONSOLE.GETCURSOR()  
Retorna:  
ROW  
COLUMN  
Exemplo:  
Position = OSL.CONSOLE.GETCURSOR()  
   
PRINT Position.ROW  
PRINT Position.COLUMN  
## **HIDECURSOR**  
OSL.CONSOLE.HIDECURSOR()  
Oculta o cursor.  
## **SHOWCURSOR**  
OSL.CONSOLE.SHOWCURSOR()  
Exibe o cursor.  
# **9. Tela**  
## **CLEAR**  
OSL.CONSOLE.CLEAR()  
Limpa a tela.  
## **CLEARLINE**  
OSL.CONSOLE.CLEARLINE()  
Limpa a linha atual.  
Também deve existir:  
OSL.CONSOLE.CLEARLINE(Row)  
para limpar uma linha específica.  
## **CLEARAREA**  
OSL.CONSOLE.CLEARAREA(  
    Top,  
    Left,  
    Bottom,  
    Right  
)  
Limpa uma área retangular.  
Exemplo:  
OSL.CONSOLE.CLEARAREA(  
    5,  
    1,  
    20,  
    80  
)  
# **10. Escrita posicionada**  
## **WRITE**  
OSL.CONSOLE.WRITE(  
    Row,  
    Column,  
    Text  
)  
Escreve texto em uma posição específica.  
Não adiciona newline.  
Exemplo:  
OSL.CONSOLE.WRITE(1, 1, "KISS")  
OSL.CONSOLE.WRITE(25, 1, "F2 Save   ESC Exit")  
# **11. PRINT e SHOW**  
Os comandos existentes continuam.  
PRINT "Hello"  
SHOW "Hello"  
PRINT adiciona quebra de linha.  
SHOW não adiciona quebra de linha.  
Eles são diferentes de:  
OSL.CONSOLE.WRITE()  
porque:  
PRINT  
    saída sequencial + newline  
   
SHOW  
    saída sequencial sem newline  
   
WRITE  
    saída posicionada  
# **12. Cores**  
## **COLOR**  
OSL.CONSOLE.COLOR(  
    Foreground,  
    Background  
)  
Exemplo:  
OSL.CONSOLE.COLOR(  
    OSL.CONSOLE.WHITE,  
    OSL.CONSOLE.BLUE  
)  
## **RESETCOLOR**  
OSL.CONSOLE.RESETCOLOR()  
Restaura as cores padrão.  
# **13. Cores disponíveis**  
As cores básicas:  
BLACK  
BLUE  
GREEN  
CYAN  
RED  
MAGENTA  
YELLOW  
WHITE  
Variantes brilhantes:  
BRIGHT_BLACK  
BRIGHT_BLUE  
BRIGHT_GREEN  
BRIGHT_CYAN  
BRIGHT_RED  
BRIGHT_MAGENTA  
BRIGHT_YELLOW  
BRIGHT_WHITE  
# **14. Keyboard API**  
A OSLANG 0.7 precisa distinguir:  
caracteres  
teclas especiais  
modificadores  
A entrada não pode depender de INPUT.  
INPUT continua sendo entrada orientada a linha.  
A console API é orientada a eventos.  
# **15. GETKEY**  
Key = OSL.CONSOLE.GETKEY()  
Bloqueia até uma tecla ser pressionada.  
Não exige ENTER.  
Exemplo:  
Key = OSL.CONSOLE.GETKEY()  
   
IF Key.KEY = OSL.CONSOLE.ESC THEN  
    RETURN  
END  
# **16. READKEY**  
Key = OSL.CONSOLE.READKEY()  
É não bloqueante.  
Se nenhuma tecla estiver disponível:  
Key = NULL  
Exemplo:  
Key = OSL.CONSOLE.READKEY()  
   
IF Key <> NULL THEN  
    HANDLEKEY(Key)  
END  
# **17. KEYAVAILABLE**  
IF OSL.CONSOLE.KEYAVAILABLE() THEN  
    Key = OSL.CONSOLE.GETKEY()  
END  
Retorna TRUE se houver um evento de teclado disponível.  
# **18. Key object**  
Cada evento de teclado retorna um objeto contendo:  
KEY  
CHAR  
CTRL  
ALT  
SHIFT  
Exemplo:  
Key = OSL.CONSOLE.GETKEY()  
   
IF Key.CHAR <> NULL THEN  
    INSERT(Key.CHAR)  
END  
Para uma tecla especial:  
IF Key.KEY = OSL.CONSOLE.UP THEN  
    MOVEUP()  
END  
# **19. Caracteres Unicode**  
CHAR é uma STRING OSLANG.  
Isso é importante para o KISS.  
O editor deve conseguir trabalhar com:  
a  
A  
ç  
á  
é  
Ω  
e outros caracteres Unicode suportados pelo terminal.  
O programa OSLANG não deve lidar com char, encoding ou estruturas específicas do .NET.  
# **20. Teclas obrigatórias**  
## **Controle**  
ENTER  
ESC  
TAB  
BACKSPACE  
DELETE  
INSERT  
SPACE  
## **Navegação**  
UP  
DOWN  
LEFT  
RIGHT  
   
HOME  
END  
   
PAGEUP  
PAGEDOWN  
## **Function keys**  
F1  
F2  
F3  
F4  
F5  
F6  
F7  
F8  
F9  
F10  
F11  
F12  
# **21. Modificadores**  
O evento deve expor:  
CTRL  
ALT  
SHIFT  
Exemplo:  
IF Key.CTRL AND Key.KEY = "S" THEN  
    SAVE()  
END  
Deve ser possível distinguir:  
S  
SHIFT+S  
CTRL+S  
ALT+S  
CTRL+SHIFT+S  
quando o terminal/host fornecer essa informação.  
# **22. ENTER**  
IF Key.KEY = OSL.CONSOLE.ENTER THEN  
    NEWLINE()  
END  
Não deve ser confundido com:  
INPUT  
# **23. TAB**  
IF Key.KEY = OSL.CONSOLE.TAB THEN  
    INSERTTAB()  
END  
# **24. BACKSPACE**  
IF Key.KEY = OSL.CONSOLE.BACKSPACE THEN  
    BACKSPACE()  
END  
# **25. DELETE**  
IF Key.KEY = OSL.CONSOLE.DELETE THEN  
    DELETE()  
END  
# **26. Terminal mode**  
Aplicações full-screen precisam assumir controle do terminal.  
## **ENTER**  
OSL.CONSOLE.ENTER()  
Deve:  
- preparar entrada por tecla;  
- configurar o modo interativo;  
- preservar o estado anterior do terminal.  
## **EXIT**  
OSL.CONSOLE.EXIT()  
Restaura o estado anterior.  
# **27. Alternate screen**  
## **ALTERNATE**  
OSL.CONSOLE.ALTERNATE(TRUE)  
entra na alternate screen.  
OSL.CONSOLE.ALTERNATE(FALSE)  
volta para a tela original.  
Isso permite:  
Shell  
  ↓  
KISS  
  ↓  
Exit  
  ↓  
Shell preservado  
# **28. Cursor e erros**  
Se ocorrer uma exceção não tratada, o runtime deve tentar restaurar:  
cursor visibility  
terminal mode  
alternate screen  
colors  
Aplicações ainda devem usar TRY/CATCH.  
Exemplo:  
TRY  
   
    OSL.CONSOLE.ENTER()  
    OSL.CONSOLE.ALTERNATE(TRUE)  
   
    RUN()  
   
CATCH ERR  
   
    OSL.CONSOLE.SHOWCURSOR()  
    OSL.CONSOLE.ALTERNATE(FALSE)  
    OSL.CONSOLE.EXIT()  
   
    PRINT ERR  
   
END  
# **29. Rendering frames**  
Aplicações full-screen não devem necessariamente emitir cada operação imediatamente.  
## **BEGINFRAME**  
OSL.CONSOLE.BEGINFRAME()  
## **ENDFRAME**  
OSL.CONSOLE.ENDFRAME()  
Finaliza e envia o frame.  
## **FLUSH**  
OSL.CONSOLE.FLUSH()  
Força a saída pendente.  
# **30. Exemplo**  
OSL.CONSOLE.BEGINFRAME()  
   
OSL.CONSOLE.CLEAR()  
   
OSL.CONSOLE.WRITE(  
    1,  
    1,  
    "KISS"  
)  
   
OSL.CONSOLE.WRITE(  
    25,  
    1,  
    "F2 Save   ESC Exit"  
)  
   
OSL.CONSOLE.ENDFRAME()  
# **31. Modelo de rendering**  
A OSLANG não expõe diretamente o framebuffer físico.  
O programa mantém seu próprio estado:  
Document  
Cursor  
Selection  
Viewport  
Status  
e o renderiza:  
Application State  
       ↓  
RENDER()  
       ↓  
BEGINFRAME()  
       ↓  
WRITE / COLOR / CLEAR  
       ↓  
ENDFRAME()  
       ↓  
Terminal  
O runtime pode otimizar internamente.  
# **32. BEEP**  
OSL.CONSOLE.BEEP()  
Produz o beep/notificação padrão do terminal, quando disponível.  
# **33. OSL.FILE**  
A OSL 0.7 precisa tornar operações de texto especialmente convenientes.  
Isso é fundamental para o KISS.  
# **34. READTEXT**  
Content = OSL.FILE.READTEXT("file.txt")  
Lê o arquivo inteiro como STRING.  
# **35. WRITETEXT**  
OSL.FILE.WRITETEXT(  
    "file.txt",  
    Content  
)  
Grava uma string como arquivo de texto.  
# **36. READLINES**  
Lines = OSL.FILE.READLINES("file.txt")  
Retorna:  
ARRAY<STRING>  
Linhas vazias devem ser preservadas.  
# **37. WRITELINES**  
OSL.FILE.WRITELINES(  
    "file.txt",  
    Lines  
)  
Grava um array de strings como arquivo de texto.  
# **38. File metadata**  
O KISS também precisa das operações:  
OSL.FILE.EXISTS(Path)  
OSL.FILE.SIZE(Path)  
OSL.FILE.DELETE(Path)  
OSL.FILE.RENAME(Source, Target)  
Caso alguma delas já exista na 0.62, não deve ser duplicada.  
# **39. OSL.APP**  
A aplicação precisa saber com quais argumentos foi iniciada.  
# **40. ARGS**  
A variável global:  
ARGS  
contém os argumentos da aplicação.  
Exemplo:  
FUNCTION MAIN()  
   
    IF COUNT(ARGS) > 0 THEN  
        FileName = ARGS[0]  
        LOAD(FileName)  
    END  
   
END  
Assim:  
kiss arquivo.txt  
produz:  
ARGS[0] = "arquivo.txt"  
# **41. EXIT**  
OSL.APP.EXIT(Code)  
Encerra a aplicação com o código informado.  
Exemplo:  
OSL.APP.EXIT(0)  
0 representa sucesso por convenção.  
# **42. String API necessária ao KISS**  
A 0.7 deve garantir que a API de STRING consiga realizar edição de texto.  
Além dos recursos já definidos anteriormente, precisamos garantir:  
COUNT()  
SUBSTR()  
LEFT()  
RIGHT()  
REPLACE()  
FIND()  
e operações equivalentes para:  
insert  
remove  
split  
join  
Caso INSERT() e REMOVE() ainda não existam na especificação anterior, eles devem ser adicionados à API de STRING.  
# **43. Array API necessária ao KISS**  
O KISS precisa conseguir manipular:  
array de linhas  
A API precisa suportar:  
COUNT()  
PUSH()  
POP()  
FINDINDEX()  
CONTAINS()  
JOIN()  
FOREACH()  
e atribuição por índice.  
Exemplo:  
Lines[10] = "Hello"  
# **44. Modelo de documento**  
O KISS pode implementar seu documento simplesmente com OSLANG:  
CLASS DOCUMENT  
   
    PUBLIC LINES  
    PUBLIC FILENAME  
    PUBLIC MODIFIED  
   
    PUBLIC CURSORROW  
    PUBLIC CURSORCOLUMN  
   
END CLASS  
Isso não faz parte da biblioteca padrão.  
É código do próprio KISS.  
# **45. Clipboard**  
O clipboard interno do editor deve ser implementado em OSLANG.  
Exemplo:  
Clipboard = []  
Nenhum recurso específico de KISS deve ser adicionado ao runtime.  
# **46. Seleção**  
Seleção também pertence ao KISS:  
SelectionStartRow  
SelectionStartColumn  
   
SelectionEndRow  
SelectionEndColumn  
A console API apenas fornece os meios para renderizar a seleção.  
# **47. Viewport**  
O editor pode manter:  
TopLine  
LeftColumn  
e converter coordenadas do documento para coordenadas do terminal.  
Não deve existir:  
KISS.SETVIEWPORT()  
no runtime.  
# **48. Cursor do editor**  
O cursor lógico pertence ao KISS.  
O cursor físico pertence à console:  
OSL.CONSOLE.SETCURSOR(  
    ScreenRow,  
    ScreenColumn  
)  
# **49. Status bar**  
Pode ser implementada utilizando:  
OSL.CONSOLE.COLOR()  
OSL.CONSOLE.WRITE()  
OSL.CONSOLE.CLEARLINE()  
Por exemplo:  
OSL.CONSOLE.COLOR(  
    OSL.CONSOLE.BLACK,  
    OSL.CONSOLE.WHITE  
)  
   
OSL.CONSOLE.WRITE(  
    Height,  
    1,  
    Status  
)  
# **50. Main loop**  
O KISS pode ser estruturado assim:  
CLASS KISS  
   
    PUBLIC FUNCTION RUN()  
   
        INITIALIZE()  
   
        WHILE Running  
   
            IF OSL.CONSOLE.RESIZED() THEN  
                RENDER()  
            END  
   
            Key = OSL.CONSOLE.GETKEY()  
   
            HANDLEKEY(Key)  
   
            RENDER()  
   
        END  
   
    END FUNCTION  
   
END CLASS  
# **51. Inicialização do KISS**  
Uma implementação possível:  
FUNCTION MAIN()  
   
    APP = NEW KISS()  
   
    APP.RUN()  
   
END  
# **52. Ciclo de vida completo**  
MAIN  
 ↓  
create KISS  
 ↓  
read ARGS  
 ↓  
load document  
 ↓  
CONSOLE.ENTER()  
 ↓  
ALTERNATE(TRUE)  
 ↓  
HIDECURSOR()  
 ↓  
RENDER()  
 ↓  
GETKEY()  
 ↓  
HANDLEKEY()  
 ↓  
update document  
 ↓  
RENDER()  
 ↓  
repeat  
 ↓  
SHOWCURSOR()  
 ↓  
ALTERNATE(FALSE)  
 ↓  
CONSOLE.EXIT()  
# **53. Exemplo de tratamento de teclas**  
PRIVATE FUNCTION HANDLEKEY(Key)  
   
    IF Key.KEY = OSL.CONSOLE.ESC THEN  
        ME.RUNNING = FALSE  
        RETURN  
    END  
   
    IF Key.CTRL AND Key.KEY = "S" THEN  
        ME.SAVE()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.UP THEN  
        ME.MOVEUP()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.DOWN THEN  
        ME.MOVEDOWN()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.LEFT THEN  
        ME.MOVELEFT()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.RIGHT THEN  
        ME.MOVERIGHT()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.BACKSPACE THEN  
        ME.BACKSPACE()  
        RETURN  
    END  
   
    IF Key.KEY = OSL.CONSOLE.DELETE THEN  
        ME.DELETE()  
        RETURN  
    END  
   
    IF Key.CHAR <> NULL THEN  
        ME.INSERT(Key.CHAR)  
        RETURN  
    END  
   
END FUNCTION  
# **54. Salvamento**  
O KISS pode implementar:  
FUNCTION SAVE()  
   
    OSL.FILE.WRITELINES(  
        ME.DOCUMENT.FILENAME,  
        ME.DOCUMENT.LINES  
    )  
   
    ME.DOCUMENT.MODIFIED = FALSE  
   
END FUNCTION  
# **55. Carregamento**  
FUNCTION LOAD(FileName)  
   
    IF OSL.FILE.EXISTS(FileName) THEN  
   
        ME.DOCUMENT.LINES =  
            OSL.FILE.READLINES(FileName)  
   
    ELSE  
   
        ME.DOCUMENT.LINES = [""]  
   
    END  
   
    ME.DOCUMENT.FILENAME = FileName  
    ME.DOCUMENT.MODIFIED = FALSE  
   
END FUNCTION  
# **56. Arquivo inexistente**  
Se o KISS for iniciado com:  
KISS NOVO.TXT  
e o arquivo não existir, o editor deve conseguir criar um documento vazio.  
Isso deve ser comportamento do KISS, não do runtime.  
# **57. Busca**  
O editor deve conseguir implementar:  
Index = Line.FIND(Search)  
Se FIND() ainda não estiver disponível na implementação da 0.62, ele deve ser incorporado antes do port.  
# **58. Substituição**  
O editor pode usar:  
Line = Line.REPLACE(  
    Search,  
    Replacement  
)  
A lógica de "Replace All" pertence ao KISS.  
# **59. Quebra de linhas**  
O editor precisa conseguir dividir uma linha:  
"HelloWorld"  
em:  
"Hello"  
"World"  
Isso pode ser implementado usando SUBSTR() e arrays.  
# **60. Junção de linhas**  
Backspace no início de uma linha precisa permitir:  
Line N  
Line N+1  
tornar-se:  
Line N + Line N+1  
A lógica é do KISS.  
# **61. Performance**  
A implementação .NET da console API deve evitar:  
flush por WRITE()  
e outras operações desnecessárias.  
BEGINFRAME() / ENDFRAME() devem permitir batching.  
O runtime pode posteriormente implementar:  
- dirty regions;  
- diff de frames;  
- redução de movimentação de cursor;  
- redução de ANSI output.  
Essas otimizações não devem alterar a API OSLANG.  
# **62. Abstração .NET recomendada**  
Internamente, o runtime pode possuir uma abstração equivalente a:  
interface IConsoleHost  
{  
    int Width { get; }  
    int Height { get; }  
   
    void SetCursor(int row, int column);  
    (int Row, int Column) GetCursor();  
   
    void Clear();  
    void ClearLine();  
    void ClearArea(...);  
   
    void Write(int row, int column, string text);  
   
    KeyEvent ReadKey();  
    KeyEvent? TryReadKey();  
    bool KeyAvailable { get; }  
   
    void SetColor(...);  
    void ResetColor();  
   
    void Enter();  
    void Exit();  
   
    void AlternateScreen(bool enabled);  
   
    void BeginFrame();  
    void EndFrame();  
    void Flush();  
   
    bool Resized { get; }  
   
    void Beep();  
}  
Isso é **implementação do runtime**, não sintaxe OSLANG.  
# **63. Independência de plataforma**  
O código:  
OSL.CONSOLE.GETKEY()  
não deve saber se o host utiliza:  
System.Console  
ANSI  
Windows Terminal  
xterm  
Linux terminal  
macOS terminal  
Essa responsabilidade é do host.  
# **64. Mouse**  
**Não é requisito do KISS.NET atual.**  
Portanto mouse não precisa entrar no núcleo da 0.7.  
Isso é especialmente importante porque o repositório atual usa mouse no **XWIN**, não no editor KISS. O README descreve o suporte a mouse como parte do Osb.Xwin, enquanto o KISS é descrito como editor de texto com setas, Ctrl+S e ESC.   
Mouse pode ser uma futura extensão de:  
OSL.CONSOLE  
sem contaminar o núcleo necessário para o KISS.  
# **65. System clipboard**  
Também não é requisito para o port inicial do KISS.  
O clipboard interno pode ser:  
Clipboard = []  
System clipboard pode ser adicionado futuramente.  
# **66. O que NÃO deve entrar na OSLANG 0.7**  
Não adicionar:  
KISS  
EDITOR  
DOCUMENT  
CURSOR  
SELECTION  
SAVE  
LOAD  
SEARCH  
REPLACE  
como keywords ou primitivas.  
Essas são responsabilidades da aplicação.  
# **67. Regra de generalização**  
Durante o port do KISS, sempre que faltar alguma coisa, classificar a necessidade:  
LANGUAGE  
STANDARD LIBRARY  
APPLICATION  
Exemplo:  
KISS precisa mover cursor  
        ↓  
STANDARD LIBRARY  
        ↓  
OSL.CONSOLE.SETCURSOR()  
Mas:  
KISS precisa salvar arquivo  
        ↓  
APPLICATION  
        ↓  
KISS.SAVE()  
E:  
KISS precisa manipular strings  
        ↓  
LANGUAGE/STANDARD LIBRARY  
        ↓  
STRING.SUBSTR()  
# **68. Critério fundamental**  
O seguinte código **não deve ser necessário**:  
KISS.SAVE()  
por parte do runtime.  
O correto é:  
FUNCTION SAVE()  
   
    OSL.FILE.WRITELINES(  
        Document.FILENAME,  
        Document.LINES  
    )  
   
END  
# **69. Acceptance Criteria**  
A OSLANG 0.7 será considerada suficiente para o port do KISS quando uma implementação OSLANG conseguir:  
-  iniciar como aplicação;   
-  receber argumentos;   
-  abrir um arquivo;   
-  criar documento vazio;   
-  ler arquivo de texto;   
-  escrever arquivo de texto;   
-  representar linhas em arrays;   
-  renderizar tela completa;   
-  posicionar cursor;   
-  ocultar/exibir cursor;   
-  detectar resize;   
-  receber caracteres individuais;   
-  receber Unicode;   
-  detectar setas;   
-  detectar Home/End;   
-  detectar PageUp/PageDown;   
-  detectar Enter;   
-  detectar Tab;   
-  detectar Backspace;   
-  detectar Delete;   
-  detectar Escape;   
-  detectar Ctrl;   
-  detectar Shift;   
-  detectar Alt;   
-  detectar F1-F12;   
-  escrever em posições arbitrárias;   
-  limpar regiões;   
-  controlar cores;   
-  executar rendering em frames;   
-  controlar alternate screen;   
-  controlar terminal mode;   
-  restaurar terminal após erro;   
-  salvar;   
-  editar linhas;   
-  pesquisar;   
-  substituir;   
-  implementar seleção;   
-  implementar clipboard interno;   
-  implementar status bar;   
-  implementar viewport;   
-  sair corretamente.   
# **70. Definition of Done**  
O teste definitivo da OSLANG 0.7 é:  
KISS.NET  
    ↓  
port  
    ↓  
KISS.OSL  
    ↓  
OSLANG runtime  
    ↓  
working full-screen editor  
O KISS.OSL deve conter a lógica do editor.  
O runtime deve conter apenas as capacidades genéricas:  
language  
filesystem  
terminal  
application lifecycle  
Não deve existir:  
KISS-specific keyword  
KISS-specific runtime function  
KISS-specific host API  
# **71. API final da 0.7**  
## **OSL.CONSOLE**  
WIDTH()  
HEIGHT()  
SIZE()  
RESIZED()  
   
SETCURSOR()  
GETCURSOR()  
   
HIDECURSOR()  
SHOWCURSOR()  
   
CLEAR()  
CLEARLINE()  
CLEARAREA()  
   
WRITE()  
   
COLOR()  
RESETCOLOR()  
   
GETKEY()  
READKEY()  
KEYAVAILABLE()  
   
ENTER()  
EXIT()  
ALTERNATE()  
   
BEGINFRAME()  
ENDFRAME()  
FLUSH()  
   
BEEP()  
## **Console constants**  
BLACK  
BLUE  
GREEN  
CYAN  
RED  
MAGENTA  
YELLOW  
WHITE  
   
BRIGHT_BLACK  
BRIGHT_BLUE  
BRIGHT_GREEN  
BRIGHT_CYAN  
BRIGHT_RED  
BRIGHT_MAGENTA  
BRIGHT_YELLOW  
BRIGHT_WHITE  
   
ENTER  
ESC  
TAB  
BACKSPACE  
DELETE  
INSERT  
SPACE  
   
UP  
DOWN  
LEFT  
RIGHT  
   
HOME  
END  
PAGEUP  
PAGEDOWN  
   
F1  
F2  
F3  
F4  
F5  
F6  
F7  
F8  
F9  
F10  
F11  
F12  
## **Key object**  
KEY  
CHAR  
CTRL  
ALT  
SHIFT  
## **OSL.FILE**  
READTEXT()  
WRITETEXT()  
READLINES()  
WRITELINES()  
   
EXISTS()  
SIZE()  
DELETE()  
RENAME()  
## **OSL.APP**  
ARGS  
EXIT()  
# **72. Exemplo mínimo completo**  
USING OSL.CONSOLE  
USING OSL.APP  
   
CLASS KISS  
   
    PRIVATE Running  
   
    PUBLIC FUNCTION RUN()  
   
        Running = TRUE  
   
        OSL.CONSOLE.ENTER()  
        OSL.CONSOLE.ALTERNATE(TRUE)  
        OSL.CONSOLE.HIDECURSOR()  
   
        TRY  
   
            WHILE Running  
   
                ME.RENDER()  
   
                Key = OSL.CONSOLE.GETKEY()  
   
                IF Key.KEY = OSL.CONSOLE.ESC THEN  
                    Running = FALSE  
                END  
   
            END  
   
        CATCH ERR  
   
            OSL.CONSOLE.SHOWCURSOR()  
            OSL.CONSOLE.ALTERNATE(FALSE)  
            OSL.CONSOLE.EXIT()  
   
            PRINT ERR  
            RETURN  
   
        END  
   
        OSL.CONSOLE.SHOWCURSOR()  
        OSL.CONSOLE.ALTERNATE(FALSE)  
        OSL.CONSOLE.EXIT()  
   
    END FUNCTION  
   
    PRIVATE FUNCTION RENDER()  
   
        Size = OSL.CONSOLE.SIZE()  
   
        OSL.CONSOLE.BEGINFRAME()  
   
        OSL.CONSOLE.CLEAR()  
   
        OSL.CONSOLE.WRITE(  
            1,  
            1,  
            "KISS - OSLANG 0.7"  
        )  
   
        OSL.CONSOLE.WRITE(  
            Size.HEIGHT,  
            1,  
            "ESC Exit"  
        )  
   
        OSL.CONSOLE.ENDFRAME()  
   
    END FUNCTION  
   
END CLASS  
   
   
FUNCTION MAIN()  
   
    APP = NEW KISS()  
   
    APP.RUN()  
   
END  
## **73. Relação com o OSB.NET atual**  
Essa especificação está alinhada com a arquitetura que já existe no repositório: Osb.Shell contém o shell e aplicações como TextEditor.cs, enquanto o Osb.Xwin possui uma camada própria de canvas/text rendering. O README também registra que o KISS atual já foi portado como editor de texto e que o XWIN possui uma infraestrutura separada para framebuffer em modo texto.   
Isso sugere uma decisão arquitetural importante para o projeto:  
OSB  
│  
├── Osb.Shell  
│      └── OSL runtime  
│             └── OSL.CONSOLE  
│  
├── KISS.OSL  
│  
└── Osb.Xwin  
       └── continuará sendo uma aplicação/host separado  
Ou seja: **não devemos transformar os requisitos específicos do XWIN em requisitos do 0.7 só porque eles existem no OSB.NET**. O KISS é um excelente primeiro *vertical slice* da nova capacidade de aplicações interativas; depois podemos evoluir OSL.CONSOLE para suportar mouse, canvas, eventos e recursos necessários ao XWIN.   
Essa separação também deixa a OSLANG muito mais interessante: **0.7 deixa de ser "a versão que suporta o KISS" e passa a ser a versão que torna possível escrever aplicações full-screen de terminal — sendo o KISS o primeiro grande consumidor dessa API.**  
   
