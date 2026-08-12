IF COMMAND$ = "" THEN AJUDA$ = "HELP"
AJUDA$ = "-" + UCASE$(COMMAND$)

RESET
B$ = "C:\OSB.CFG"
a1 = FREEFILE
OPEN B$ FOR INPUT AS #a1
    WHILE NOT EOF(a1)
        LINE INPUT #a1, A$
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
3 CLOSE #a1

COLOR LETRA, FUNDO

Arquivo$ = Diretorio$ + "\Help\OSB.Hlp"

a1 = FREEFILE
contagem = 0
OPEN Arquivo$ FOR INPUT AS #a1
    WHILE NOT EOF(a1)
        LINE INPUT #1, A$
        A$ = UCASE$(A$)
        IF A$ = "-ENDOFFILE" THEN
            PRINT "T¢pico da ajuda n∆o encontrado."
            END
        END IF
        IF A$ = AJUDA$ THEN
            WHILE A$ <> "-ENDCOMMAND"
                LINE INPUT #1, A$
                IF A$ <> "-ENDCOMMAND" THEN
                    PRINT A$
                    contagem = contagem + 1
                    IF contagem = 22 THEN
                        PRINT "Pressione qualquer tecla para continuar"
                        r$ = INPUT$(1)
                        contagem = 0
                    END IF
                END IF
            WEND
        END IF
    WEND
CLOSE #a1

