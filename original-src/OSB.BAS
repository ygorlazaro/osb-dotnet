'Declaraá∆o de vari†veis globais
'Diret¢rios de instalaá∆o do OSB
DIM SHARED Drive$, Diretorio$
'Cor de letra e de fundo
DIM SHARED Fundo$, Letra$
'Se aceita o logo de entrada e o NumLock ligado
DIM SHARED Logo$, Num$
'Pasta Temp do OSB
DIM SHARED Temp$
DIM SHARED SystemCfg$

'Declaraá∆o de rotinas
DECLARE SUB Games (Comando$)
DECLARE SUB Boot ()
DECLARE SUB About ()
DECLARE SUB Nada ()
DECLARE SUB Pass (Config1$)
DECLARE SUB Config ()
DECLARE SUB Aplic (Comando$)
DECLARE SUB Calc ()
DECLARE SUB Command (Comando$)
DECLARE SUB Ajuda (Arq$)
DECLARE SUB Starts (Chamado)
'Inicia o processo de boot
Boot

'Captura de erros
23 RESET

SELECT CASE ERR
        CASE 5
                PRINT "Chamada de funá∆o inv†lida."
                RESUME NEXT
        CASE 6
                PRINT "Estouro de divis∆o."
                RESUME NEXT
        CASE 7
                PRINT "Sem mem¢ria."
                RESUME NEXT
        CASE 11
                PRINT "Divis∆o por zero."
                RESUME NEXT
        CASE 25
                PRINT "Configuraá∆o/Hardware em falta"
                RESUME NEXT
        CASE 27
                PRINT "Sem papel."
                RESUME NEXT
        CASE 51
                PRINT "Estouro interno."
                RESUME NEXT
        CASE 52
                PRINT "Nome ou n£mero de arquivo n∆o encontrado."
                Command ""
        CASE 53
                PRINT "Arquivo n∆o encontrado."
                RESUME NEXT
        CASE 54
                PRINT "Modo de arquivo inv†lido."
                RESUME NEXT
        CASE 57
                PRINT "Erro de despositivo I/O. ê necess†rio formatar o disco"
                CHDRIVE "c:"
                RESUME NEXT
        CASE 58
                PRINT "Arquivo j† existente."
                RESUME NEXT
        CASE 61
                PRINT "Disco cheio. Substitua o disco e tente novamente."
                RESUME NEXT
        CASE 62
                RESUME NEXT
        CASE 64
                PRINT "Nome de arquivo invalido."
                RESUME NEXT
        CASE 67
                PRINT "Excesso de arquivos."
                RESUME NEXT
        CASE 68
                PRINT "Dispositivo n∆o dispon°vel."
                RESUME NEXT
        CASE 70
                PRINT "Permiss∆o negada."
                RESUME NEXT
        CASE 71
                PRINT "Disco n∆o est† pronto."
15              INPUT "(A)nular, (R)epetir:", Esc$
                Esc$ = UCASE$(Esc$)
                SELECT CASE Esc$
                CASE "A"
                        INPUT "Entre com a letra do drive: ", a$
                        CHDRIVE a$
                CASE "R"
                        CASE ELSE
                        GOTO 15
                        END SELECT
                        RESUME
        CASE 72
                PRINT "Erro de disco. Substitua o disco e tente novamente."
                RESUME NEXT
        CASE 75
                PRINT "Erro de acesso ao caminho/arquivo."
                RESUME NEXT
        CASE 76
                PRINT "Erro do caminho"
                RESUME NEXT
        CASE ELSE
                PRINT "Erro inesperado. Anote o n£mero do erro e entre em contato em "
                PRINT "http://www.osb.rg3.net ou ygoronline@yahoo.com.br"
                PRINT
                PRINT "Erro n£mero: "; ERR
                Command ""
                RESUME
END SELECT

SUB Ajuda (Arq$)
    'Chama o arquivo de ajuda do comando pedido
    SHELL Diretorio$ + "\Help.com " + Arq$
END SUB

SUB Aplic (Comando$)
    'Executa o comando Aplic para chamar aplicativos do OSB
    IF Comando$ <> "" THEN
        SHELL Diretorio$ + "\Aplic.com " + Comando$
    ELSE
        SHELL Diretorio$ + "\Aplic.com"
    END IF
END SUB

SUB Boot
'Inicia o processo de boot
'Limpa a tela
CLS
'Reinicia os arquivos
RESET

'Là o arquivo C:\OSB.CFG e retira algumas informaá‰es.
'Salva essas informaá‰es nas vari†veis globais.
PRINT "Iniciando o processo de boot do OSB"
B$ = "C:\OSB.CFG"
A1 = FREEFILE
OPEN B$ FOR INPUT AS #A1
    WHILE NOT EOF(A1)
        LINE INPUT #A1, a$
        SELECT CASE UCASE$(a$)
            'Cor de letra
            CASE "[FORECOLOR]"
                LINE INPUT #1, a$
                Letra = VAL(a$)
            'Cor de fundo
            CASE "[BACKCOLOR]"
                LINE INPUT #1, a$
                Fundo = VAL(a$)
            'Diret¢rio do sistema
            CASE "[SYSTEM]"
                LINE INPUT #1, a$
                Diretorio$ = a$
                'Drive do sistema
                Drive$ = LEFT$(Diretorio$, 2)
                CHDRIVE Drive$
                CHDIR Diretorio$
            'Se aceita o logo de entrada
            CASE "[LOGO]"
                LINE INPUT #1, Logo$
            'Se aceita o NumLock ligado
            CASE "[NUM]"
                LINE INPUT #1, Num$
            'Mensagem de boot
            CASE "[MESSAGE]"
                LINE INPUT #1, MESSAGE$
            END SELECT
    WEND
3 CLOSE #A1

SystemCfg$ = Diretorio$ + "\Conf\System.Cfg"
OPEN SystemCfg$ FOR INPUT AS #1
        WHILE NOT EOF(1)
                LINE INPUT #1, a$
                SELECT CASE UCASE$(a$)
                        CASE "[MOUSE]"
                                LINE INPUT #1, a$
                                IF UCASE$(a$) = "TRUE" THEN
                                        PRINT "Iniciando o mouse"
                                        SHELL Diretorio$ + "\System\Mouse.com"
                                END IF
                        CASE "[CLOCK]"
                                LINE INPUT #1, a$
                                IF UCASE$(a$) = "TRUE" THEN
                                        PRINT "Iniciando o relogio"
                                        SHELL Diretorio$ + "\System\Clock.com /E"
                                END IF
                END SELECT
        WEND
CLOSE #1


PRINT "Definindo as cores"
COLOR Letra, Fundo

PRINT "Definindo os diretorios"
Temp$ = Diretorio$ + "\Temp\"

PRINT "Iniciando o NumLock"
IF Num$ = "TRUE" THEN SHELL "NUM +"

IF Logo$ = "TRUE" THEN SHELL "LOGO OSB.IMG"
1 CLS
PRINT "Data: "; DATE$
PRINT "Hora: "; TIME$
PRINT
PRINT MESSAGE$

'Chama a rotina Starts
Starts 0

'Inicia a interface do usu†rio
Command ""

END SUB

SUB Command (Comando$)
'No caso de erro, v† para a rotina de captura de erros
ON ERROR GOTO 23

2
'Exibe o prompt do usu†rio
IF Comando$ = "" THEN
    PRINT CURDIR$
    LINE INPUT "[@] ", Comando$
ELSE
    Chamado = 1
END IF

'Retira os espaáos do comando e coloca em mai£scula
Comando$ = LTRIM$(RTRIM$(UCASE$(Comando$)))

'Se comando for RPT, executa o £ltimo comando v†lido no sistema.
IF Comando$ = "RPT" THEN
        Comando$ = Copia$
END IF

'Copia o comando usado, para executar caso o RPT seja chamado.
Copia$ = Comando$

'Se digitar Drive:, alterna para o drive especificado.
IF LEN(Comando$) = 2 AND RIGHT$(Comando$, 1) = ":" THEN
        CHDRIVE Comando$
        Y = 1
END IF

'Se o comando for ., executa um aplicativo externo.
IF LEFT$(Comando$, 1) = "." THEN
        'Verifica o comando externo a ser executado
        a = LEN(Comando$)
        a = a - 1
        Comando$ = RIGHT$(Comando$, a)

        'Se n∆o digitar nada, pede ajuda do comando .
        IF Comando$ = "" THEN
                Ajuda "."
        ELSE
            'Executa o comando externo
            PRINT "Executando um programa externo (fora do kernel)."
            SHELL Comando$
            PRINT "Final da execuá∆o"
        END IF
        Y = 1
        Comando$ = "." + Comando$
END IF

'Se o comando for seguido de /?, executa a ajuda do comando
IF RIGHT$(Comando$, 2) = "/?" THEN
        'Verifica o comando a ser pesquisado na ajuda
        a = LEN(Comando$)
        a = a - 2
        Arq$ = LEFT$(Comando$, a)
        'Chama a ajuda do comando
        Ajuda RTRIM$(Arq$)
        Y = 1
        GOTO 13
END IF

'Mostra informaá‰es do sistema operacional
IF Comando$ = "ABOUT" THEN
        'Executa o arquivo About.com
        Comando$ = Diretorio$ + "\About.com"
        SHELL Comando$

        'Imprime algumas informaá‰es adicionais
        CLS
        PRINT "Para mais informaá‰es sobre o OSB, o projeto OSB Brasil, sobre como obter e"
        PRINT "ajudar o projeto, entre em contato conosco."
        PRINT
        PRINT "Por site: http://www.osb.rg3.net"
        PRINT "Por e-mail: ygoronline@yahoo.com.br"
        PRINT "Por ICQ: 105417016"
        PRINT "Por MSN: ygoronline@hotmail.com"
        PRINT "Por Yahoo Messenger: ygoronline@yahoo.com.br"
        PRINT
        PRINT "Ygor Lazaro"
        Y = 1
END IF

'Executa a Sub Aplic
IF LEFT$(Comando$, 5) = "APLIC" THEN
        a = LEN(Comando$)
        IF a = 5 THEN
                Aplic ""
        ELSE
                Comando$ = RIGHT$(Comando$, a - 6)
                Aplic Comando$
                Comando$ = Copia$
        END IF
        Y = 1
END IF

'Executa o calend†rio
IF LEFT$(Comando$, 3) = "CAL" THEN
    IF LEN(Comando$) = 3 THEN
        SHELL "C:\OSB\APLIC\CAL.COM"
    ELSE
        SHELL "C:\OSB\APLIC\CAL.COM " + RIGHT$(Comando$, LEN(Comando$) - 4)
    END IF
    Y = 1
END IF

IF LEFT$(Comando$, 2) = "CD" THEN
        g = 0
        a = LEN(Comando$)
        a = a - 2
        COMANDO1$ = RIGHT$(Comando$, a)
        IF COMANDO1$ = "" THEN
                Ajuda "CD"
        ELSE
            a = LEN(COMANDO1$)
            IF COMANDO1$ = ".." OR COMANDO1$ = " .." THEN CHDIR "..": g = 1
            IF COMANDO1$ = "\" OR COMANDO1$ = " \" THEN CHDIR "\": g = 1
            IF g = 0 THEN
                COMANDO1$ = RIGHT$(COMANDO1$, a - 1)
                CHDIR COMANDO1$
            END IF
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 3) = "CLS" THEN
        CLS
        Y = 1
END IF

IF Comando$ = "COLOR" THEN
    SHELL Diretorio$ + "\Color.com"
    B$ = "C:\OSB.CFG"
    A1 = FREEFILE
    OPEN B$ FOR INPUT AS #A1
        WHILE NOT EOF(A1)
            LINE INPUT #A1, a$
            SELECT CASE UCASE$(a$)
                CASE "[FORECOLOR]"
                    LINE INPUT #1, a$
                    Letra = VAL(a$)
                CASE "[BACKCOLOR]"
                    LINE INPUT #1, a$
                    Fundo = VAL(a$)
            END SELECT
        WEND
    CLOSE #A1
    COLOR Letra, Fundo
    Y = 1
    CLS

END IF

IF LEFT$(Comando$, 6) = "CONFIG" THEN
        Config
        Y = 1
END IF

IF LEFT$(Comando$, 4) = "COPY" THEN
        LINE INPUT "Entre com o arquivo de origem: ", Origem$
        LINE INPUT "Entre com o arquivo de destino: ", Destino$
        Linhas = 0
        OPEN Origem$ FOR INPUT AS #1
        OPEN Destino$ FOR OUTPUT AS #2
                WHILE NOT EOF(1)
                        LINE INPUT #1, a$
                        PRINT #2, a$
                        Linhas = Linhas + 1
                WEND
        CLOSE #1, #2
        PRINT STR$(Linhas) + "copiadas."
        Y = 1
END IF

IF Comando$ = "DATE" THEN
        PRINT "Formato aceito: DD/MM/AAAA"
        PRINT "Data atual: "; DATE$
        LINE INPUT "Insira uma nova data: ", DATA$
        IF DATA$ <> "" THEN
        DATE$ = DATA$
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 3) = "DIR" THEN
        a = LEN(Comando$)
        PRINT "Exibindo o conte£do do diret¢rio: "
        IF a = 3 THEN
                FILES
        END IF
        IF a > 3 THEN
                a = a - 3
                Comando$ = RIGHT$(Comando$, a)
                Comando$ = LTRIM$(Comando$)
                IF a$ = "\" THEN
                    Ajuda "DIR"
                ELSE
                    FILES Comando$
                END IF
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 5) = "ERASE" THEN
        a = LEN(Comando$)
        IF a = 5 THEN
                Ajuda "ERASE"
        ELSE
        LINE INPUT "Vocà tem certeza que deseja apagar o(s) arquivo(s)? ", Sn$
        Sn$ = UCASE$(Sn$)
        SELECT CASE Sn$
                CASE "S"
                a = a - 6
                Comando$ = RIGHT$(Comando$, a)
                IF Comando$ = "." THEN Comando$ = "*.*"
                PRINT "Excluindo..."
                KILL Comando$
        END SELECT
        END IF
        Y = 1
END IF

IF Comando$ = "EXIT" THEN
    LINE INPUT "Vocà tem certeza que deseja sair (S/N)? ", Sn$
    Sn$ = UCASE$(Sn$)
    IF Sn$ = "S" THEN
        PRINT "Finalizando o relogio."
        SHELL Diretorio$ + "\System\Clock.Com /U"
        PRINT "Finalizando os arquivos."
        RESET
        PRINT "Finalizando o driver do mouse."
        SHELL Diretorio$ + "\System\Mouse.com /U"
        PRINT "Finalizando o kernel."
        PRINT "OSB 0.2 encerrado."
        END
    END IF
    Y = 1
END IF

IF LEFT$(Comando$, 5) = "GAMES" THEN
        a = LEN(Comando$)
        IF a = 5 THEN
                Games ""
        ELSE
                Comando$ = RIGHT$(Comando$, a - 6)
                Games Comando$
                Comando$ = Copia$
                END IF
        Y = 1
END IF

IF LEFT$(Comando$, 4) = "HELP" AND Y = 0 THEN
        a = LEN(Comando$)
        IF a <= 5 THEN
                Ajuda "HELP"
        ELSE
                a = a - 5
                Comando$ = RIGHT$(Comando$, a)
                Ajuda Comando$
        END IF
        Y = 1
        GOTO 13
END IF

IF LEFT$(Comando$, 4) = "KISS" THEN
        a = LEN(Comando$)
        a = a - 4
        Comando$ = RIGHT$(Comando$, a)
        SHELL "C:\OSB\APlic\Kiss" + Comando$
        Y = 1
END IF

IF LEFT$(Comando$, 2) = "MD" THEN
        a = LEN(Comando$)
        a = a - 2
        Comando$ = RIGHT$(Comando$, a)
        IF Comando$ = "" THEN
                Ajuda "MD"
        ELSE
                Comando$ = LTRIM$(Comando$)
                MKDIR Comando$
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 5) = "PRINT" THEN
        a = LEN(Comando$)
        a = a - 5
        Comando$ = RIGHT$(Comando$, a)
        IF Comando$ = "" THEN
                Ajuda "PRINT"
        ELSE
                PRINT "Imprimindo " + Comando$
                OPEN Comando$ FOR INPUT AS #1
                        WHILE NOT EOF(1)
                                LINE INPUT #1, a$
                                LPRINT a$
                        WEND
                CLOSE #1
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 2) = "RD" THEN
        a = LEN(Comando$)
        a = a - 3
        Comando$ = RIGHT$(Comando$, a)
        IF Comando$ = "" THEN
                Ajuda "RD"
        ELSE
                Comando$ = LTRIM$(Comando$)
                RMDIR Comando$
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 3) = "REN" THEN
        INPUT "Entre com o nome antigo: ", name$
        INPUT "Entre com o nome novo: ", nov$
        IF name$ <> "" AND nov$ <> "" THEN
                NAME name$ AS nov$
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 4) = "SIZE" THEN
        a = LEN(Comando$)
        a = a - 4
        Comando$ = RIGHT$(Comando$, a)
        IF Comando$ = "" THEN
                Ajuda "SIZE"
        ELSE
                Comando$ = LTRIM$(Comando$)
                OPEN Comando$ FOR INPUT AS #1
                        a = LOF(1) / 1024
                CLOSE #1
                PRINT a; " KiloBytes"
        END IF
        Y = 1
END IF

IF Comando$ = "TIME" THEN
        PRINT "Formato aceito: HH:MM:SS"
        PRINT "Hora atual: "; TIME$
        INPUT "Entre com a nova hora: ", HORA$
        IF HORA$ <> "" THEN
                TIME$ = HORA$
        END IF
        Y = 1
END IF

IF LEFT$(Comando$, 4) = "TREE" THEN
        SHELL Diretorio$ + "\Aplic\Tree.com"
        Y = 1
END IF

IF LEFT$(Comando$, 4) = "TYPE" THEN
        a = LEN(Comando$)
        IF a <= 5 THEN
                Ajuda Comando$
        ELSE
                a = a - 5
                Comando$ = RIGHT$(Comando$, a)
                OPEN Comando$ FOR INPUT AS #1
                        a = 1
                        B = 1
                        WHILE NOT EOF(1)
                                LINE INPUT #1, a$
                                SELECT CASE a
                                        CASE IS <= 20
                                                PRINT a$
                                        CASE ELSE
                                                PRINT "-----Pressione qualquer tecla----"
                                                R$ = INPUT$(1)
                                                a = 1
                                END SELECT
                        a = a + 1
                        B = B + 1
                        WEND
                CLOSE #1
                PRINT B - 1; " linhas"
        END IF
        Y = 1
END IF

IF Comando$ = "VER" THEN
        PRINT "OSB Versao 0.2"
        PRINT "http://www.osb.rg3.net"
        PRINT
        PRINT "Digite ABOUT para mais informaá‰es"
        Y = 1
END IF

IF Comando$ = "X" THEN
        a = 1
        PRINT "Qualquer programador pode criar novas interfaces para o OSB. A interface"
        PRINT "padrao Ç simples, criada em VBDOS, mas serve para exemplificar o intuito"
        PRINT "dos X, como s∆o chamados no OSB."
        PRINT
        PRINT "Interfaces gr†ficas instaladas no sistema:"
        PRINT
        a = 1
        Arq$ = "C:\OSB\Conf\Interf.cfg"
        OPEN Arq$ FOR INPUT AS #1
            WHILE NOT EOF(1)
                LINE INPUT #1, a$
                PRINT LTRIM$(STR$(a)) + " - " + a$
                a = a + 1
            WEND
        CLOSE #1

        PRINT "0 - Nenhuma"
        PRINT
        INPUT "Entre com sua escolha: ", escolha$
        Esc = VAL(escolha$)
        a = 1

        OPEN Arq$ FOR INPUT AS #1
            WHILE NOT EOF(1)
                LINE INPUT #1, a$
                IF a = Esc THEN GOTO 19
                a = a + 1
            WEND
        PRINT "N∆o encontrado!"
        CLOSE #1
        Y = 1
        GOTO 13
19      CLOSE #1
        CHDIR Diretorio$ + "\" + a$
        SHELL a$
        Y = 1
END IF

13 IF Y = 0 AND Comando$ <> "" THEN
        PRINT "Comando ou nome de arquivo inv†lido!"
ELSE
        Y = 0
END IF
PRINT
Comando$ = ""
IF Chamado <> 1 THEN
    GOTO 2
ELSE
    Chamado = 0
    Starts 1
END IF
END SUB

SUB Config
        SHELL Diretorio$ + "\System\Config.Com"
        CLS
END SUB

SUB Games (Comando$)
IF Comando$ <> "" THEN Esc$ = Comando$: GOTO 18
CLS
PRINT "***************************"
PRINT "Games instalados ao OSB 2.0"
PRINT "***************************"
OPEN "C:\OSB\CONF\GAMES.CFG" FOR INPUT AS #1
WHILE NOT EOF(1)
LINE INPUT #1, a$
PRINT a$
WEND
CLOSE #1
PRINT
INPUT "Entre com sua escolha (<NADA> para sair): ", Esc$
18 IF Esc$ <> "" THEN
        Esc$ = UCASE$(Esc$)
        OPEN "C:\OSB\CONF\GAMES.CFG" FOR INPUT AS #1
        WHILE NOT EOF(1)
        LINE INPUT #1, a$
        a$ = UCASE$(a$)
        IF Esc$ = a$ THEN
                B = 1
                END IF
        WEND
        IF B = 1 THEN
                a$ = "C:\OSB\GAMES\" + Esc$
                SHELL a$
                END IF
        END IF
CLOSE #1

END SUB

SUB Starts (Chamado)
'Executa os comandos listados no arquivo Start.cfg da pasta Conf

IF Chamado = 1 THEN GOTO 4
Arquivo$ = Diretorio$ + "\Conf\Start.cfg"

OPEN Arquivo$ FOR INPUT AS #1
    WHILE NOT EOF(1)
4       LINE INPUT #1, a$
        Command (a$)
    WEND
CLOSE #1
END SUB

