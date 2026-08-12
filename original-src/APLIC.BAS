RESET
B$ = "C:\OSB.CFG"
A1 = FREEFILE
OPEN B$ FOR INPUT AS #A1
    WHILE NOT EOF(A1)
        LINE INPUT #A1, A$
        A$ = UCASE$(A$)
        SELECT CASE A$
            CASE "[BACKCOLOR]"
                LINE INPUT #1, A$
                FUNDO = VAL(A$)
            CASE "[FORECOLOR]"
                LINE INPUT #1, A$
                LETRA = VAL(A$)
            CASE "[SYSTEM]"
                LINE INPUT #1, A$
                Diretorio$ = A$
            END SELECT
    WEND
3 CLOSE #A1

COLOR LETRA, FUNDO
IF COMMAND$ <> "" THEN
    SHELL Diretorio$ + "\Aplic\" + COMMAND$
    END
END IF

ARQUIVO$ = Diretorio$ + "\Conf\Aplic.cfg"


PRINT "*********************************"
PRINT "Aplicativos instalados ao OSB 0.2"
PRINT "*********************************"

A1 = FREEFILE
Contagem = 0
OPEN ARQUIVO$ FOR INPUT AS #A1
    WHILE NOT EOF(A1)
        LINE INPUT #1, A$
        A$ = UCASE$(A$)
        IF A$ <> "-ENDCOMMAND" AND A$ <> "-ENDOFFILE" THEN
            PRINT A$
        END IF
        IF Contagem = 22 THEN
            PRINT "Pressione qualquer tecla para continuar"
            r$ = INPUT$(1)
            Contagem = 0
        END IF
        Contagem = Contagem + 1
    WEND
CLOSE #A1

Final:
PRINT
INPUT "Entre com sua escolha (<NADA> para sair): ", esc$

SHELL Diretorio$ + "\Aplic\" + esc$

