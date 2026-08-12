DEFINT A-Z

DECLARE SUB centertype (row!, text$)
DECLARE SUB colorandom ()
DECLARE SUB drawhang ()
DECLARE SUB pause (Seconds!)
DECLARE SUB typeit (text$)
DECLARE SUB wipe ()

SCREEN 12

som = 1

pi# = 22 / 7

mainmenu:
used$ = ""
phrase$ = ""
triesleft = 6

CLS

pause .5

drawhang

menu$(1) = "1) Inicia o jogo para um jogador"
menu$(2) = "2) Inicia o jogo para dois jogadores"
menu$(3) = "3) Ver/Trocar op‡äes"
menu$(4) = "4) Fechar Hangman"

LOCATE 29, 10: COLOR 7: PRINT "Hangman (C) 1998 KeiProductions";

ttext$ = "H  A  N  G  M  A  N": n = 9
colorandom

FOR letters = 1 TO LEN(ttext$) STEP 3
 FOR drop = 1 TO 8
 LOCATE drop, n + letters: PRINT MID$(ttext$, letters, 1);
 FOR p = 1 TO 10000: NEXT p
 LOCATE drop, n + letters: PRINT " ";
 NEXT drop
 LOCATE drop, n + letters: PRINT MID$(ttext$, letters, 1);
NEXT letters

z = 9: ON TIMER(1) GOSUB colorchange
TIMER ON

COLOR 15
FOR sm = 1 TO 4
 FOR typet = 1 TO LEN(menu$(sm))
 LOCATE sm + 10, 9 + typet: COLOR 7: PRINT MID$(menu$(sm), typet, 1);
 FOR p = 1 TO 10000: NEXT p
 LOCATE sm + 10, 9 + typet: COLOR 15: PRINT MID$(menu$(sm), typet, 1);
 FOR p = 1 TO 5000: NEXT p
 NEXT typet
NEXT sm

getmainchoice:
board$ = INKEY$
IF board$ = "" THEN GOTO getmainchoice
IF board$ = "1" THEN GOTO oneplay
IF board$ = "2" THEN GOTO twoplay
IF board$ = "3" THEN GOTO options
IF board$ = "4" THEN TIMER OFF: GOTO finished
GOTO getmainchoice

options:                                  
                                              
colorandom

ttext$ = "O  P  €  å  E  S": n = 9

FOR a = 1 TO LEN(ttext$)
LOCATE 17, 2: PRINT RIGHT$(ttext$, a)
FOR p = 1 TO 8000: NEXT p
NEXT a

FOR m = 2 TO 9
LOCATE 17, m: PRINT ttext$;
FOR p = 1 TO 8000: NEXT p
LOCATE 17, m: PRINT STRING$(LEN(ttext$), 255)
NEXT m
LOCATE 17, m: PRINT ttext$;

redrawit:
IF som = 1 THEN som$ = "On" ELSE som$ = "Off"
LOCATE 19, 10: COLOR 15: PRINT "1) Som/M£sica: "; som$; " "
LOCATE 21, 10: PRINT "Pressione 1 para trocar Som/Musica entre on/off"
LOCATE 22, 10: PRINT "Pressione Esc para retornar para o menu"

geto:
z$ = INKEY$
IF z$ = "" THEN GOTO geto
IF z$ = "1" THEN
 IF som = 1 THEN som = 0 ELSE som = 1
 GOTO redrawit
END IF
IF z$ = CHR$(27) THEN
 FOR down = 250 TO 350
  LINE (2, down)-(400, down), 0
  FOR p = 1 TO 4000: NEXT p
 NEXT down
 GOTO getmainchoice
END IF
GOTO geto

oneplay:       
NumberOfPhrases = 24

DATA "O amor e fogo que arde sem se ver","Voce esta falando comigo?"
DATA "Esta na hora!","Vejo voce depois!","Isso nao e o maximo?"
DATA "Voce conseguiu adivinar a frase!", "Experimete com dois jogadores."
DATA "Hora do cha das cinco!", "Nao olhe para mim!","Eu nao sou isso."
DATA "Nao fui eu.","Procume mais sobre isso...","O que esta no mundo?"
DATA "Preciso de um plano.","Por que esse bolo? Aniversario!"
DATA "As coisas nao sao tao ruins...","Protegido contra o BUG do ano 2000"
DATA "E so o amor que conhece o que e verdade","Ela disse adeus"
DATA "Esta na hora!","Vejo voce depois!","Isso nao e o maximo?"
DATA "Feliz natal e um prospero ano novo", "Agora com novo sabor limao"

rp = INT(RND * NumberOfPhrases) + 1

RESTORE

FOR gt = 1 TO rp - 1
READ nonsense$
NEXT gt

READ phrase$

copyofphrase$ = LEFT$(phrase$, LEN(phrase$))

GOTO mainloop

twoplay:         

TIMER OFF
wipe

z = 9: ON TIMER(1) GOSUB colorchange
TIMER ON

LINE (0, 0)-(639, 479), 9, B
GOTO gettphrase

colorchange:      
z = z + 1: IF z = 15 THEN z = 9
LINE (0, 0)-(639, 479), z, B
LINE (1, 1)-(638, 478), z, B
RETURN

gettphrase:
COLOR 15: centertype 2, "Jogador 1, entre com a frase"
ps = 2
chars$ = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.,!?'1234567890 "

DO
LOCATE 4, ps: PRINT "Û";
loopk:
z$ = INKEY$: IF z$ = "" THEN GOTO loopk
IF z$ = CHR$(13) AND phrase$ <> "" THEN EXIT DO
IF z$ = CHR$(8) AND ps > 2 THEN
 LOCATE 4, ps - 1: PRINT "  "; : LOCATE 4, ps - 1: ps = ps - 1
 phrase$ = LEFT$(phrase$, (LEN(phrase$) - 1))
END IF
 IF INSTR(chars$, z$) THEN
 IF ps = 41 THEN GOTO loopk
 phrase$ = phrase$ + z$: LOCATE 4, ps: PRINT z$; : ps = ps + 1
END IF
LOOP


LOCATE 4, ps: PRINT " ";

copyofphrase$ = LEFT$(phrase$, LEN(phrase$))

COLOR 15
centertype 6, "Agora, jogador 2, tente adivinhar"
colorandom
centertype 8, "Pressione uma tecla para iniciar"

WHILE INKEY$ = "": WEND

mainloop:

TIMER OFF
wipe
COLOR 15

LINE (0, 0)-(639, 479), 12, B
LINE (1, 1)-(638, 478), 6, B
LINE (2, 2)-(637, 477), 6, B

ps = INT(41 - (LEN(phrase$) * 2) / 2)
LOCATE 2, ps

letters$ = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
FOR a = 1 TO LEN(phrase$)
 sc$ = MID$(phrase$, a, 1)
 IF INSTR(letters$, sc$) THEN PRINT " _";  ELSE PRINT " "; sc$;
NEXT a

drawhang

LOCATE 25, 5: COLOR 9: PRINT "LETRAS USADAS"

startagain:

LOCATE 15, 5: COLOR 9: PRINT "TENTATIVAS RESTANTES"; : COLOR 15: PRINT triesleft;
LOCATE 26, 5: COLOR 15: PRINT used$
LOCATE 8, 5: COLOR 15: PRINT "Entre com uma letra: Û"; :

getletter:

z$ = INKEY$
IF z$ = "" THEN GOTO getletter
IF z$ = CHR$(27) THEN wipe: GOTO mainmenu
IF INSTR(letters$, z$) THEN
 IF INSTR(used$, z$) THEN GOTO getletter
 LOCATE , POS(0) - 1: PRINT z$;
 GOTO checkforit
END IF
GOTO getletter

checkforit:                            
c1$ = z$
IF UCASE$(c1$) = c1$ THEN c2$ = LCASE$(c1$) ELSE c2$ = UCASE$(c1$)
                     
used$ = used$ + LCASE$(z$)

flag = 0
FOR c = 1 TO LEN(phrase$)
sctn$ = MID$(phrase$, c, 1)
IF sctn$ = c1$ THEN
 LOCATE 2, ps - 1 + (c * 2)
 PRINT c1$
 flag = 1
 ELSEIF sctn$ = c2$ THEN
 LOCATE 2, ps - 1 + (c * 2)
 PRINT c2$;
 flag = 1
END IF
NEXT c

IF flag THEN GOTO seeifyouwon

IF som THEN PLAY "o0 c"
                                           
triesleft = triesleft - 1
                                           
SELECT CASE triesleft
CASE 5
 CIRCLE (400, 165), 30, 15
 CIRCLE (390, 158), 3, 15: PAINT (390, 158), 9, 15
 CIRCLE (410, 158), 3, 15: PAINT (410, 158), 9, 15
 CIRCLE (400, 185), 15, 15, 0, pi#
CASE 4
 LINE (400, 195)-(400, 200), 15
 LINE (400, 200)-(380, 210), 15
 LINE (400, 200)-(420, 210), 15
 PSET (380, 210), 15: DRAW "c15 d100 r40 u100"
 PAINT (381, 211), 10, 15
CASE 3
 LINE (380, 210)-(350, 270), 15
CASE 2
 LINE (420, 210)-(450, 270), 15
CASE 1
 PSET (380, 310): DRAW "c15 d100 r20 u100"
 PAINT (381, 311), 9, 15
CASE 0
 PSET (420, 310): DRAW "c15 d100 l20 u100"
 PAINT (419, 311), 9, 15
 GOTO youlose
END SELECT

GOTO startagain
                                                   
seeifyouwon:
FOR a = 1 TO 80
IF SCREEN(2, a) = ASC("_") THEN GOTO startagain
NEXT a
                                                   
IF som THEN PLAY "mb o2 t240 cc1" ELSE pause .4
                                                   
pause 1

FOR a = 1 TO 8000
colorandom
z = INT(RND * 30) + 1
y = INT(RND * 71) + 1
LOCATE z, y: PRINT " VECÒ VENCEU! ";
IF INKEY$ <> "" THEN wipe: GOTO mainmenu
FOR p = 1 TO 100: NEXT p
NEXT a

wipe
GOTO mainmenu
                        
finished:
FOR B = 0 TO 480
LINE (0, B)-(640, B), 0
pause 1D-27
NEXT
SYSTEM

youlose:
IF som THEN PLAY "mf o2 t240 c2 < g. g a-2 g2... b2 > c2" ELSE pause 1
CLS : COLOR 9: centertype 1, "VOCÒ PERDEU!"
COLOR 15: centertype 3, "Frase correta: " + copyofphrase$
COLOR 14: centertype 5, "Pressione uma tecla para retornar"

WHILE INKEY$ = "": WEND

wipe
GOTO mainmenu

SUB centertype (row!, text$)
col = INT(41 - LEN(text$) / 2)
LOCATE row!, col
FOR tp = 1 TO LEN(text$)
 PRINT MID$(text$, tp, 1);
 FOR p = 1 TO 8000: NEXT p
NEXT tp
END SUB

SUB colorandom
RANDOMIZE TIMER
r% = INT(RND * 6) + 9
COLOR r%
END SUB

SUB drawhang
PSET (500, 460)
DRAW "c15r100"
PSET (550, 460)
DRAW "c15u400l150d75"
END SUB

DEFSNG A-Z
SUB pause (Seconds)
waitforit = TIMER
DO WHILE TIMER < waitforit + Seconds: LOOP
END SUB

DEFINT A-Z
SUB wipe
RANDOMIZE TIMER
z = INT(RND * 3) + 1

IF z = 1 THEN
 LINE (0, 479)-(100, 479), 0
 u = 480
 FOR d = 0 TO 480
  LINE (0, 0)-(640, d), 0
  LINE (640, 480)-(0, u), 0
  u = u - 1
 NEXT d

 CLS : LOCATE 1, 1
 pause .1
 EXIT SUB

ELSEIF z = 2 THEN

 FOR a = 240 TO 0 STEP -1
 LINE (320, 240)-(640, a), 0
 FOR p = 1 TO 50: NEXT p
 NEXT a

 FOR a = 640 TO 0 STEP -1
 LINE (320, 240)-(a, 0), 0
 FOR p = 1 TO 50: NEXT p
 NEXT a

 FOR a = 0 TO 480
 LINE (320, 240)-(0, a), 0
 FOR p = 1 TO 50: NEXT p
 NEXT a

 FOR a = 0 TO 640
 LINE (320, 240)-(a, 480), 0
 FOR p = 1 TO 50: NEXT p
 NEXT a

 FOR a = 480 TO 240 STEP -1
 LINE (320, 240)-(640, a), 0
 FOR p = 1 TO 50: NEXT p
 NEXT a

EXIT SUB

ELSEIF z = 3 THEN

 z = 640
 FOR a = 0 TO 640

 LINE (a, 0)-(a, 240), 0
 LINE (z, 241)-(z, 480), 0
 z = z - 1
 FOR p = 1 TO 100: NEXT p
 NEXT a
 FOR p = 1 TO 1000: NEXT p

END IF

END SUB

