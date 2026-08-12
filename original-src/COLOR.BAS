CLS
PRINT "Tabela de cores:"
PRINT
COLOR 0: PRINT "0 - Preto"
COLOR 1: PRINT "1 - Azul escuro"
COLOR 2: PRINT "2 - Verde"
COLOR 3: PRINT "3 - Ciano"
COLOR 4: PRINT "4 - Vermelho"
COLOR 5: PRINT "5 - Margenta"
COLOR 6: PRINT "6 - Marrom"
COLOR 7: PRINT "7 - Branco"
COLOR 8: PRINT "8 - Cinza"
COLOR 9: PRINT "9 - Azul claro"
COLOR 10: PRINT "10 - Verde claro"
COLOR 11: PRINT "11 - Cinza escuro"
COLOR 12: PRINT "12 - Vermelho claro"
COLOR 13: PRINT "13 - Margenta claro"
COLOR 14: PRINT "14 - Amarelo"
COLOR 15: PRINT "15 - Branco alta intensidade"
PRINT
5 INPUT "Entre com a cor das letras: ", Letra$
Letra = VAL(Letra$)
IF Letra >= 16 OR Letra < 0 THEN GOTO 5
6 INPUT "Entre com a cor do fundo: ", Fundo$
Fundo = VAL(Fundo$)
IF Fundo >= 16 OR Fundo < 0 THEN GOTO 6
A = 1

COLOR Letra, Fundo

B$ = "C:\OSB.CFG"
TEMP$ = "C:\TEMPOSB.TMP"
OPEN B$ FOR INPUT AS #1
OPEN TEMP$ FOR OUTPUT AS #2
    WHILE NOT EOF(1)
        LINE INPUT #1, A$
        A$ = UCASE$(A$)
        IF A$ = "[FORECOLOR]" THEN
            PRINT #2, A$
            PRINT #2, Letra
            LINE INPUT #1, A$
        ELSEIF A$ = "[BACKCOLOR]" THEN
            PRINT #2, A$
            PRINT #2, Fundo
            LINE INPUT #1, A$
        ELSE
            PRINT #2, A$
        END IF
    WEND
CLOSE #1, #2

KILL B$
NAME TEMP$ AS B$


