2
p1 = 1
p2 = 99999
keyboard:
PALETTE 0, 40
CLS : COLOR 4, 11: LOCATE 10, 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     A     B       C        D       E       F        G       H    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     I     J       K        L       M       N        O       P    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     Q     R       S        T       U       V        W       X    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "                            Y       Z                             "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "  0     1       2       3      4      5     6     7     8    9    "
LOCATE , 7
PRINT "                                                                  "
COLOR 10, 0
LOCATE 1, 15: PRINT
LOCATE 3, 23: PRINT "  - |^+-> Datilografia <-+^|  - (Vers∆o 1.0) "
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
COLOR 11
LOCATE 23, 1: PRINT " Status : Pressione SPACE para comeáar"
COLOR 15, 0
LOCATE 5, 2: PRINT " No quadro abaixo, vocà pode observar todas as letras do alfabeto e os n£meros"
PRINT " de 0 a 9. Quando uma dessas letras ou n£meros ficarem amarelos, Ç sinal que"
PRINT " vocà deve apert†-la no seu teclado. Vocà tem 3 segundos para apertar cada tecla"
PRINT "Ao conseguir 10 acertos vocà recebe o diploma, e se errar 2 vezes, Ç reprovado."
DO
IF INKEY$ = " " THEN 1
LOOP

1 COLOR 0, 0
CLS : COLOR 4, 11: LOCATE 10, 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     A     B       C        D       E       F        G       H    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     I     J       K        L       M       N        O       P    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "     Q     R       S        T       U       V        W       X    "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "                            Y       Z                             "
LOCATE , 7
PRINT "                                                                  "
LOCATE , 7
PRINT "  0     1       2       3      4      5     6     7     8    9    "
LOCATE , 7
PRINT "                                                                  "
COLOR 10, 0
LOCATE 1, 15: PRINT "Programa pàgo na internet para intergraá∆o ao BasicOS"
LOCATE 3, 23: PRINT "  - |^+-> Datilografia <-+^|  - (Vers∆o 1.0) "
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
COLOR 11
LOCATE 23, 1: PRINT " Status : Pressione SPACE para comeáar"
COLOR 15, 0
LOCATE 5, 2: PRINT " No quadro abaixo, vocà pode observar todas as letras do alfabeto e os n£meros"
PRINT " de 0 a 9. Quando uma dessas letras ou n£meros ficarem amarelos, Ç sinal que"
PRINT " vocà deve apert†-la no seu teclado. Vocà tem 3 segundos para apertar cada tecla"
PRINT "Ao conseguir 10 acertos vocà recebe o diploma, e se errar 2 vezes, Ç reprovado."
  COLOR 11
  LOCATE 23, 1: PRINT " Status : Aguarde...                         "
  FOR A = p1 TO p2
  NEXT
  LOCATE 23, 1: PRINT " Status : Pronto, aperte a tecla correspondente."
  COLOR 14, 11
  RANDOMIZE TIMER
  rx = INT(RND * 35)
COLOR 14
'***
IF acertos = 10 THEN
  COLOR 11, 0
  LOCATE 23, 1: PRINT " Status : Vocà ganhou o diploma!                   "
  FOR op = p1 TO p2: NEXT op: GOTO diploma
END IF
IF erros = 2 THEN
  COLOR 11, 0
  LOCATE 23, 1: PRINT " Status : Infelizmente vocà foi reprovado!         "
FOR xaguar = p1 TO p2: NEXT: GOTO reprovado
END IF
IF rx = 0 THEN GOTO nA
IF rx = 1 THEN GOTO nB
IF rx = 2 THEN GOTO nC
IF rx = 3 THEN GOTO nD
IF rx = 4 THEN GOTO nE
IF rx = 5 THEN GOTO nF
IF rx = 6 THEN GOTO nG
IF rx = 7 THEN GOTO nH
IF rx = 8 THEN GOTO nI
IF rx = 9 THEN GOTO nJ
IF rx = 10 THEN GOTO nK
IF rx = 11 THEN GOTO nL
IF rx = 12 THEN GOTO nM
IF rx = 13 THEN GOTO nN
IF rx = 14 THEN GOTO nO
IF rx = 15 THEN GOTO nP
IF rx = 16 THEN GOTO nQ
IF rx = 17 THEN GOTO nR
IF rx = 18 THEN GOTO nS
IF rx = 19 THEN GOTO nT
IF rx = 20 THEN GOTO nU
IF rx = 21 THEN GOTO nV
IF rx = 22 THEN GOTO nW
IF rx = 23 THEN GOTO nX
IF rx = 24 THEN GOTO nY
IF rx = 25 THEN GOTO nZ
IF rx = 26 THEN GOTO n0
IF rx = 27 THEN GOTO n1
IF rx = 28 THEN GOTO n2
IF rx = 29 THEN GOTO n3
IF rx = 30 THEN GOTO n4
IF rx = 31 THEN GOTO n5
IF rx = 32 THEN GOTO n6
IF rx = 33 THEN GOTO n7
IF rx = 34 THEN GOTO n8
IF rx = 35 THEN GOTO n9
nA:
COLOR 14
LOCATE 11, 12: PRINT "A"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 30 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
END

nB:
COLOR 14
LOCATE 11, 18: PRINT "B"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 48 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
END
nC:
COLOR 14
 LOCATE 11, 26: PRINT "C"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 46 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nD:
COLOR 14
 LOCATE 11, 35: PRINT "D"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 32 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nE:
COLOR 14
 LOCATE 11, 43: PRINT "E"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 18 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nF:
COLOR 14
 LOCATE 11, 51: PRINT "F"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 33 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nG:
COLOR 14
 LOCATE 11, 60: PRINT "G"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 34 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nH:
COLOR 14
 LOCATE 11, 68: PRINT "H"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 35 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nI:
COLOR 14
LOCATE 13, 12: PRINT "I"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 23 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
END
nJ:
COLOR 14
LOCATE 13, 18: PRINT "J"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 36 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
END
nK:
COLOR 14
 LOCATE 13, 26: PRINT "K"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 37 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nL:
COLOR 14
 LOCATE 13, 35: PRINT "L"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 38 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nM:
COLOR 14
 LOCATE 13, 43: PRINT "M"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 50 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nN:
COLOR 14
 LOCATE 13, 51: PRINT "N"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 49 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nO:
COLOR 14
 LOCATE 13, 60: PRINT "O"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 24 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nP:
COLOR 14
 LOCATE 13, 68: PRINT "P"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 25 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nQ:                                              
COLOR 14
 LOCATE 15, 12: PRINT "Q"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 16 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nR:
COLOR 14
 LOCATE 15, 18: PRINT "R"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 19 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nS:
COLOR 14
 LOCATE 15, 26: PRINT "S"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 31 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nT:
COLOR 14
 LOCATE 15, 35: PRINT "T"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 20 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nU:
COLOR 14
 LOCATE 15, 43: PRINT "U"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 22 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nV:
COLOR 14
 LOCATE 15, 51: PRINT "V"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 47 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nW:
COLOR 14
 LOCATE 15, 60: PRINT "W"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 17 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nX:
COLOR 14
 LOCATE 15, 68: PRINT "X"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 45 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nY:
COLOR 14
 LOCATE 17, 35: PRINT "Y"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 21 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
nZ:
COLOR 14
 LOCATE 17, 43: PRINT "Z"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 44 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n0:
COLOR 14
 LOCATE 19, 9: PRINT "0"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 11 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n1:
COLOR 14
 LOCATE 19, 15: PRINT "1"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 2 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n2:
COLOR 14
 LOCATE 19, 23: PRINT "2"
SLEEP 1
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 3 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n3:
COLOR 14
 LOCATE 19, 31: PRINT "3"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 4 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n4:
COLOR 14
 LOCATE 19, 38: PRINT "4"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 5 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n5:
COLOR 14
 LOCATE 19, 45: PRINT "5"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 6 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n6:
COLOR 14
 LOCATE 19, 51: PRINT "6"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 7 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n7:
COLOR 14
 LOCATE 19, 57: PRINT "7"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 8 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n8:
COLOR 14
 LOCATE 19, 63: PRINT "8"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 9 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END
n9:
COLOR 14
 LOCATE 19, 68: PRINT "9"
SLEEP 1.4
DO
LOOP UNTIL INKEY$ = ""
IN = INP(&H60)
IF IN = 10 THEN
PLAY "BC"
acertos = acertos + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
ELSE
BEEP
erros = erros + 1
COLOR 12, 0
LOCATE 21, 10: PRINT "  Acertos : "; acertos; "                   Erros : "; erros
FOR n = p1 TO p2: NEXT n: GOTO 1
END IF
 END

diploma:
CLS
COLOR 15, 0
PALETTE 0, 0
LOCATE 3, 22: PRINT " Acertos : "; acertos; "           Erros : "; erros
LOCATE 7, 22: PRINT " ParabÇns! Vocà conseguiu o diploma, est† aprovado!"
IF erros > 0 THEN
PRINT
PRINT "         Tente novamente, veja se consegue o diploma sem errar"
PRINT "                         nenhuma vez!"
PRINT
ELSE
PRINT
PRINT "         Vocà Ç um craque na datilografia, pois "
PRINT " conseguiu 10 acertos e nenhum erro! ParabÇns , '"
PRINT " vocà consegue repetir a faáanha ??"
PRINT
END IF
END
reprovado:
CLS
COLOR 0, 0
PALETTE 0, 0
COLOR 15, 0
LOCATE 3, 22: PRINT " Acertos : "; acertos; "           Erros : "; erros
LOCATE 7, 22: PRINT " Infelizmente, vocà n∆o conseguiu fazer 10 acertos."
IF acertos >= 5 THEN
PRINT
PRINT "   Mas chegou perto, faltou pouco! Tente novamente."
PRINT
ELSE
PRINT
PRINT " Como vi, vocà n∆o Ç muito r†pido na datilografia,"
PRINT " mas s¢ Ç preciso um pouco mais de treino!!"
PRINT
END
END IF

