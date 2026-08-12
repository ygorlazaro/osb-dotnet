DECLARE SUB DOCINIT (D%)
DECLARE SUB DOCOPN (F$, D%)
DECLARE SUB ALARM ()
DECLARE SUB CHECKMARK (D%, SH%)
DECLARE SUB CLIPBOARD ()
DECLARE SUB SHOWDOC (D%)
DECLARE SUB INFOBAR (D%)
DECLARE SUB CUTTEXT (D%)
DECLARE SUB ORDERMARK (D%, LA%, LZ%, CA%, CZ%)
DECLARE SUB NOISE ()
DECLARE SUB DELETETEXT (D%)
DECLARE SUB PUTLINE (D%, T$)
DECLARE FUNCTION DELLINE$ (D%)
DECLARE SUB PARMSINIT (D%)
DECLARE FUNCTION EDDOC% (D%)
DECLARE SUB INSLINE (D%, S$)
DECLARE SUB LINECAT (D%, L%)
DECLARE SUB SAVEQRY (D%)
DECLARE SUB LOADDOC (D%)
DECLARE SUB SAVEDOC (D%)
DECLARE SUB SETMARK (D%, SH%)
DECLARE SUB PASTETEXT (D%)
DECLARE SUB HELP ()
DECLARE FUNCTION GETKEY% ()
DECLARE FUNCTION GETLINE$ (D%)
DECLARE FUNCTION GETSTR$ (P$, D$, M%)
DECLARE FUNCTION MSG% (M$, P$)
DECLARE SUB SHOWLINE (D%, L%, S%)
  CLEAR ' $DYNAMIC
  TYPE TXT
    L AS STRING * 80
  END TYPE
  TYPE PARMS
    INS AS INTEGER
    TOP AS INTEGER
    LIN AS INTEGER
    CRS AS INTEGER
    TOT AS INTEGER
    MLA AS INTEGER
    MCA AS INTEGER
    MLZ AS INTEGER
    MCZ AS INTEGER
    WID AS INTEGER
  END TYPE
  DIM SHARED D0(800) AS TXT
  DIM SHARED D1(800) AS TXT
  DIM SHARED D2(800) AS TXT
  DIM SHARED CFG(3) AS PARMS
  DIM SHARED DOC$(6)
  DOC$(1) = COMMAND$
  DOC$(2) = ""
  DOCINIT 0
  DOCINIT 1
  DOCINIT 2
  DOC$(4) = ""
  DOC$(5) = "INS"
  IF DOC$(1) <> "" THEN
    DOCOPN DOC$(1), 1
  ELSE
    DOC$(1) = "Sem_Nome.txt"
  END IF
  ED% = 1
  WHILE ED% <> 0
    ED% = EDDOC%(ED%)
  WEND

  COLOR 2, 0
  CLS
  SYSTEM
WHATTHE:
  DOC$(3) = STR$(ERR)
RESUME NEXT

REM $STATIC
SUB ALARM
  FOR T% = 0 TO 25
    SOUND 2200 + 2 * T%, .1
    SOUND 2225 + T%, .05
  NEXT
END SUB

SUB CHECKMARK (D%, SH%)
  IF SH% THEN
    IF CFG(D%).MCA = 0 THEN
      CFG(D%).MLA = CFG(D%).LIN
      CFG(D%).MLZ = CFG(D%).LIN
      CFG(D%).MCA = CFG(D%).CRS
      CFG(D%).MCZ = CFG(D%).CRS
    END IF
   ELSE
    CFG(D%).MCA = 0
  END IF
END SUB

SUB CLIPBOARD
  CFG(0).LIN = 0
  CFG(0).CRS = 1
  K% = 0
  DOC$(0) = "AREA DE TRANSFERENCIAS    "
  WHILE K% <> 27
    L% = CFG(0).LIN
    CFG(0).MCA = 1
    CFG(0).MCZ = 80
    CFG(0).MLA = L%
    CFG(0).MLZ = L%
    SHOWDOC 0
    INFOBAR 0
    K% = GETKEY%
    SELECT CASE K%
       CASE -71
      CFG(0).LIN = 0
      CFG(0).TOP = 0
       CASE -79
      L% = CFG(0).TOT - 23
      IF L% < 0 THEN L% = 0
      CFG(0).TOP = L%
      CFG(0).LIN = CFG(0).TOT - 1
CASE -73:
      IF L% <> 0 THEN
        C% = L% - 24
        CFG(0).LIN = -C% * (C% >= 0)
        C% = CFG(0).TOP - 24
        CFG(0).TOP = -C% * (C% >= 0)
      END IF
       CASE -81
      T% = CFG(D%).TOT - 1
      P% = T% - 23
      L% = L% + 24
      IF L% <= (T% + 23) THEN
        IF L% > T% THEN L% = T%
        CFG(0).LIN = L%
        L% = CFG(0).TOP + 24
        IF L% > P% THEN L% = P%
        CFG(0).TOP = -L% * (L% >= 0)
      END IF
       CASE -72
      L% = L% - 1
      IF L% >= 0 THEN
        CFG(0).LIN = L%
        IF CFG(0).TOP > L% THEN CFG(0).TOP = L%
      END IF
       CASE -80
      L% = L% + 1
      IF L% < CFG(0).TOT THEN
        CFG(0).LIN = L%
        T% = CFG(0).TOP
        IF L% > (T% + 23) THEN CFG(0).TOP = T% + 1
      END IF
    END SELECT
  WEND
END SUB

SUB CUTTEXT (D%)
  IF CFG(D%).MCA = 0 THEN
    T% = MSG%("Sem textos marcados", "")
    EXIT SUB
  END IF
  ORDERMARK D%, LA%, LZ%, CA%, CZ%
  DOCINIT 0
  R% = CFG(D%).LIN
  I% = LZ% - LA%
  CFG(0).TOT = I% + 1
  FOR L% = 0 TO I%
    CFG(0).LIN = L%
    CFG(D%).LIN = LA% + L%
    T$ = GETLINE$(D%)
    IF L% = I% THEN T$ = LEFT$(T$, CZ%)
    IF L% = 0 THEN T$ = MID$(T$, CA%)
    D0(L%).L = T$
  NEXT
  CFG(D%).LIN = R%
  NOISE
END SUB

SUB DELETETEXT (D%)
  IF CFG(D%).MCA = 0 THEN
    T% = MSG%("Sem textos marcados", "")
    EXIT SUB
  END IF
  ORDERMARK D%, LA%, LZ%, CA%, CZ%
  L% = LZ% - LA%
  R% = LA%
  CFG(D%).CRS = CA%
  CFG(D%).MCA = 0
  CFG(D%).LIN = LA%
  T$ = MID$(LEFT$(" " + GETLINE$(D%), CA%), 2)
  CFG(D%).LIN = LZ%
  P$ = RTRIM$(MID$(GETLINE$(D%), CZ% + 1))
  B$ = T$ + P$
  IF LA% = LZ% THEN
    IF B$ = "" THEN
      B$ = DELLINE$(D%)
    ELSE
      PUTLINE D%, B$
    END IF
  END IF
  IF LZ% > LA% THEN
    IF P$ = "" THEN
      LZ% = LZ% + 1
     ELSE
      PUTLINE D%, P$
    END IF
    IF T$ <> "" THEN
      CFG(D%).LIN = LA%
      LA% = LA% + 1
      PUTLINE D%, T$
    END IF
  END IF
  IF LA% < LZ% THEN
    L% = LZ% - LA%
    Z% = CFG(D%).TOT - L%
    CFG(D%).TOT = Z%
    FOR T% = LA% TO Z%
      IF D% = 1 THEN D1(T%).L = D1(T% + L%).L
      IF D% = 2 THEN D2(T%).L = D2(T% + L%).L
    NEXT
    FOR T% = Z% TO 799
      IF D% = 1 THEN D1(T%).L = ""
      IF D% = 2 THEN D2(T%).L = ""
    NEXT
  END IF
  CFG(D%).LIN = R%
  R% = R% - 1
  IF CFG(D%).TOT < 0 THEN CFG(D%).TOT = 1
  IF R% + 25 > CFG(D%).TOT THEN R% = CFG(D%).TOT - 24
  CFG(D%).TOP = R% * -(R% >= 0)
  NOISE
END SUB

FUNCTION DELLINE$ (D%)
  L% = CFG(D%).LIN
  E% = CFG(D%).TOT - 1
  IF D% = 1 THEN
    DELLINE$ = RTRIM$(D1(CFG(1).LIN).L)
    FOR T% = L% TO E% - 1
      D1(T%).L = D1(T% + 1).L
    NEXT
    D1(E%).L = SPACE$(80)
  END IF
  IF D% = 2 THEN
    DELLINE$ = RTRIM$(D2(CFG(2).LIN).L)
    FOR T% = L% TO E% - 1
      D2(T%).L = D2(T% + 1).L
    NEXT
    D2(E%).L = SPACE$(80)
  END IF
  CFG(D%).TOT = E% - (E% = L%)
  SHOWDOC D%
END FUNCTION

SUB DOCINIT (D%)
  FOR T% = 0 TO 799
    IF D% = 0 THEN D0(T%).L = ""
    IF D% = 1 THEN D1(T%).L = ""
    IF D% = 2 THEN D2(T%).L = ""
  NEXT
  PARMSINIT D%
END SUB

SUB DOCOPN (F$, D%)
  ON ERROR GOTO WHATTHE
  T% = 1
  DOC$(3) = "ok"
  IF F$ <> "" THEN OPEN F$ FOR INPUT AS #1
  T$ = "Erro de arquivo :: " + F$ + " :: Pressione algo"
  IF DOC$(3) <> "ok" THEN
    T% = MSG%(T$, "")
    EXIT SUB
  END IF
  WHILE T% = 1
    L% = 0
    DOCINIT D%
    WHILE ((L% < 800) AND (NOT EOF(1)))
      LINE INPUT #1, P$
      P$ = RTRIM$(P$)
      CFG(D%).LIN = L%
      WHILE INSTR(P$, CHR$(9)) <> 0
        P% = INSTR(P$, CHR$(9))
        T$ = MID$(P$, P% + 1)
        T$ = "   " + T$
        IF P% > 1 THEN T$ = LEFT$(P$, P% - 1) + T$
        P$ = T$
      WEND
      IF LEN(P$) > 79 THEN
        IF L% < 799 THEN
          PUTLINE D%, LEFT$(P$, 79)
          P$ = MID$(P$, 80)
          L% = L% + 1
          CFG(D%).LIN = L%
        END IF
        P$ = LEFT$(P$, 79)
      END IF
      PUTLINE D%, P$
      L% = L% + 1
    WEND
    T$ = "Arquivo muito grande. Abrir na proxima seá∆o (S/N)?"
    IF (NOT EOF(1)) THEN
      T% = MSG%(T$, "sn")
     ELSE
      T% = 0
    END IF
  WEND
  CLOSE 1
  CFG(D%).TOT = L%
  CFG(D%).LIN = 0
  ALARM
  DOC$(D%) = UCASE$(F$)
END SUB

FUNCTION EDDOC% (D%)
  DEF SEG = 0
  EDDOC% = D%
  IF D% = 0 THEN GOTO CYCLE
  SHOWDOC D%
  EDIT$ = "YES"
  S$ = SPACE$(80)
  WHILE EDIT$ = "YES"
    T$ = GETLINE$(D%) + S$
    L% = CFG(D%).LIN
    C% = CFG(D%).CRS
    SHOWDOC D%
    INFOBAR D%
    LOCATE L% - CFG(D%).TOP + 2, C%, 1, 13 * CFG(D%).INS, 13
    K% = GETKEY%
    SH% = ((PEEK(&H417) AND 3) <> 0)
    W% = CFG(D%).WID
    IF K% = 9 THEN
      T$ = LEFT$(T$, C% - 1) + SPACE$(9) + MID$(T$, C%)
      K% = 32
      C% = C% + 9
    END IF
    SELECT CASE K%
     CASE 32 TO 254
      IF CFG(D%).INS <> 0 THEN MID$(T$, C% + 1) = MID$(T$, C%)
      MID$(T$, C%) = CHR$(K%)
      T$ = RTRIM$(T$)
      IF (LEN(T$) > W%) THEN
        P$ = MID$(T$, W% + 1)
        T$ = LEFT$(T$, W%)
        T% = 0
        WHILE T% = 0
          R$ = RIGHT$(T$, 1)
          L% = LEN(T$)
          T% = INSTR("*+\]}:)/- ", R$) - (L% < (W% * .75))
          IF T% = 0 THEN
            P$ = R$ + P$
            T$ = LEFT$(T$, L% - 1)
          END IF
        WEND
        PUTLINE D%, T$
        L% = CFG(D%).LIN
        T% = LEN(T$)
        IF L% < 799 THEN
          CFG(D%).LIN = L% + 1
          INSLINE D%, P$
        END IF
        IF C% > W% AND L% < 799 THEN
          C% = C% - T%
          L% = L% + 1
        END IF
        CFG(D%).LIN = L%
        IF C% > W% AND L% > 788 THEN C% = T%
       ELSE
        PUTLINE D%, T$
      END IF
      CFG(D%).CRS = C% + 1
     CASE 8
      L% = CFG(D%).LIN
      T% = LEN(RTRIM$(T$))
      IF (C% = 1) AND (L% = 0) THEN GOTO CYCLE
      IF C% > 1 THEN
        IF T% <> 0 THEN MID$(T$, C% - 1) = MID$(T$, C%) + " "
        CFG(D%).CRS = C% - 1
        PUTLINE D%, T$
       ELSE
        LINECAT D%, L% - 1
      END IF
       CASE -83
      T% = LEN(RTRIM$(T$))
      IF C% <= T% THEN
        MID$(T$, C%) = MID$(T$, C% + 1) + " "
        PUTLINE D%, T$
       ELSE
        LINECAT D%, CFG(D%).LIN
      END IF
       CASE -82
      T$ = DOC$(6)
      IF SH% THEN
        IF T$ <> "" THEN
          INSLINE D%, T$
          CFG(D%).CRS = 1
        END IF
       ELSE
        CFG(D%).INS = ABS(CFG(D%).INS - 1)
        NOISE
      END IF
     CASE 13
      L% = CFG(D%).LIN
      IF L% >= 799 THEN GOTO CYCLE
      IF C% = 1 THEN
        P$ = ""
       ELSE
        P$ = LEFT$(T$, C% - 1)
      END IF
      PUTLINE D%, P$
      CFG(D%).LIN = L% + 1
      INSLINE D%, MID$(T$, C%)
      CFG(D%).CRS = 1
       CASE -61, -38
      SAVEQRY D%
      LOADDOC D%
      GOTO CYCLE
       CASE -60, -31
      SAVEDOC D%
     CASE 27, -45
      EDIT$ = "NO"
       CASE -94
      IF DOC$(1) = "" THEN DOC$(1) = "Sem_Nome.txt"
      EDDOC% = 1
      GOTO CYCLE
       CASE -95
      IF DOC$(2) = "" THEN DOC$(2) = "Sem_Nome.txt"
      EDDOC% = 2
      GOTO CYCLE
       CASE -96
      CLIPBOARD
     CASE 25
      DOC$(6) = DELLINE$(D%)
      CFG(D%).CRS = 1
       CASE -71
      CHECKMARK D%, SH%
      CFG(D%).CRS = 1
      SETMARK D%, SH%
       CASE -79
      CHECKMARK D%, SH%
      C% = LEN(RTRIM$(T$)) + 1
      CFG(D%).CRS = -C% * (C% < 81) - 80 * (C% > 80)
      SETMARK D%, SH%
       CASE -119
      CHECKMARK D%, SH%
      CFG(D%).CRS = 1
      CFG(D%).LIN = CFG(D%).TOP
      SETMARK D%, SH%
       CASE -117
      CHECKMARK D%, SH%
      C% = CFG(D%).TOP + 23
      T% = CFG(D%).TOT - 1
      IF C% >= T% THEN C% = T%
      CFG(D%).LIN = C%
      T$ = RTRIM$(GETLINE$(D%))
      T% = LEN(T$)
      CFG(D%).CRS = T% - (T% < 80)
      SETMARK D%, SH%
       CASE -132
      CHECKMARK D%, SH%
      CFG(D%).TOP = 0
      CFG(D%).LIN = 0
      CFG(D%).CRS = 1
      SETMARK D%, SH%
       CASE -118
      CHECKMARK D%, SH%
      C% = CFG(D%).TOT
      CFG(D%).TOP = -(C% - 24) * (C% > 23)
      CFG(D%).LIN = C% - 1
      CFG(D%).CRS = 1
      SETMARK D%, SH%
       CASE -73
      CHECKMARK D%, SH%
      IF L% = 0 THEN GOTO CYCLE
      C% = L% - 24
      CFG(D%).LIN = -C% * (C% >= 0)
      C% = CFG(D%).TOP - 24
      CFG(D%).TOP = -C% * (C% >= 0)
      SETMARK D%, SH%
       CASE -81
      CHECKMARK D%, SH%
      T% = CFG(D%).TOT - 1
      P% = T% - 23
      L% = CFG(D%).LIN + 24
      IF L% > (T% + 23) THEN GOTO CYCLE
      IF L% > T% THEN L% = T%
      CFG(D%).LIN = L%
      L% = CFG(D%).TOP + 24
      IF L% > P% THEN L% = P%
      CFG(D%).TOP = -L% * (L% >= 0)
      SETMARK D%, SH%
       CASE -72
      CHECKMARK D%, SH%
      L% = L% - 1
      IF L% < 0 THEN GOTO CYCLE
      CFG(D%).LIN = L%
      IF CFG(D%).TOP > L% THEN CFG(D%).TOP = L%
      SETMARK D%, SH%
       CASE -80
      CHECKMARK D%, SH%
      L% = L% + 1
      IF L% >= CFG(D%).TOT THEN GOTO CYCLE
      CFG(D%).LIN = L%
      T% = CFG(D%).TOP
      IF L% > (T% + 23) THEN CFG(D%).TOP = T% + 1
      SETMARK D%, SH%
       CASE -75
      CHECKMARK D%, SH%
      IF C% = 1 THEN GOTO CYCLE
      CFG(D%).CRS = C% - 1
      SETMARK D%, SH%
       CASE -77
      CHECKMARK D%, SH%
      IF C% > 79 THEN GOTO CYCLE
      CFG(D%).CRS = C% + 1
      SETMARK D%, SH%
       CASE -46
      CUTTEXT D%
       CASE -32
      DELETETEXT D%
       CASE -25
      PASTETEXT D%
       CASE -59, -35
      HELP
       CASE -115
      CFG(D%).WID = W% + (W% > 10)
       CASE -116
      CFG(D%).WID = W% - (W% < 79)
    END SELECT
  WEND
  T$ = CHR$(13) + "yn " + CHR$(27)
  SAVEQRY D%
  EDDOC% = -D% * (MSG("Sair KISSED (Y/N)?", T$) > 2)
CYCLE:
  NOISE
  DEF SEG
END FUNCTION

FUNCTION GETKEY%
  T$ = ""
  WHILE T$ = ""
    T$ = INKEY$
  WEND
  GETKEY% = ASC(T$)
  IF ASC(T$) = 0 THEN GETKEY% = -ASC(MID$(T$, 2))
END FUNCTION

FUNCTION GETLINE$ (D%)
  GETLINE$ = ""
  IF D% = 1 THEN GETLINE$ = D1(CFG(1).LIN).L
  IF D% = 2 THEN GETLINE$ = D2(CFG(2).LIN).L
END FUNCTION

FUNCTION GETSTR$ (P$, D$, M%)
  PCOPY 0, 1
  SCREEN , , 1, 1
  L% = 7 + LEN(P$) + M%
  INS% = 1
  Y% = 40 - L% \ 2
  X% = 11
  COLOR 9, 1
  LOCATE X%, Y%
  FOR T% = 1 TO 3
    LOCATE , Y%
    PRINT STRING$(L%, CHR$(176))
  NEXT
  LOCATE X% + 1, Y% + 2
  COLOR 15, 4
  PRINT " "; P$; " ";
  X% = CSRLIN
  Y% = POS(0)
  PRINT SPACE$(M% + 1);
  D$ = LEFT$(D$ + SPACE$(M%), M%)
  B$ = CHR$(29)
  P% = (LEN(RTRIM$(D$)) MOD M%) + 1
  WHILE T% <> 13
    LOCATE , Y%, 0
    COLOR 14, 4
    PRINT D$;
    COLOR 1, 7
    LOCATE , Y% + P% - 1, INS%, 13, 13
    PRINT MID$(D$, P%, 1); B$;
    T% = GETKEY%
    SELECT CASE T%
     CASE 32 TO 127
      IF INS% <> 0 THEN D$ = LEFT$(LEFT$(D$, P%) + MID$(D$, P%), M%)
      MID$(D$, P%) = CHR$(T%)
      IF P% < M% THEN P% = (P% MOD M%) + 1
     CASE 8
      IF P% > 1 THEN MID$(D$, P% - 1) = MID$(D$, P%) + " "
      P% = P% + (P% > 1)
      IF P% = 1 THEN SOUND 1000, .1
     CASE 27
      D$ = ""
      T% = 13
       CASE -82
      INS% = ABS(INS% - 1)
       CASE -83
      MID$(D$, P%) = MID$(D$, P% + 1) + " "
       CASE -75
      P% = ((P% - 2 + M%) MOD M%) + 1
       CASE -77
      P% = (P% MOD M%) + 1
     CASE 25
      D$ = SPACE$(M%)
      P% = 1
       CASE -71
      P% = 1
       CASE -79
      P% = LEN(RTRIM$(D$))
      P% = P% - (P% <> M%)
    END SELECT
  WEND
  SCREEN , , 0, 0
  GETSTR$ = RTRIM$(D$)
END FUNCTION

SUB HELP
  SCREEN , , 1, 1
  COLOR 11, 1
  CLS
  T$ = STRING$(80, CHR$(177))
  FOR T% = 1 TO 25
    LOCATE T%, 1
    PRINT T$;
  NEXT
  COLOR 14, 0
  LOCATE 1, 27: PRINT "Vers∆o compat°vel do OSB"
  LOCATE 3, 7, 0
  PRINT "____TECLAS NORMAIS____________"
  LOCATE , 7
  PRINT " Seta esquerda  (Cursor esq.) "
  LOCATE , 7
  PRINT " Seta direita  (Cursor Dir.)  "
  LOCATE , 7
  PRINT " Seta acima    (Cursor Acima) "
  LOCATE , 7
  PRINT " Seta abaixo   (Cursor abaixo)"
  LOCATE , 7
  PRINT " Page UP       (Pagina acima) "
  LOCATE , 7
  PRINT " Page Down    (Pagina abaixo) "
  LOCATE , 7
  PRINT " Home                (Inicio) "
  LOCATE , 7
  PRINT " End                  (Final) "
  LOCATE , 7
  PRINT " Insert            (Inserá∆o) "
  LOCATE , 7
  PRINT " Delete             (Deletar) "
  LOCATE , 7
  PRINT " F1                   (Ajuda) "
  LOCATE , 7
  PRINT " F2             (Abrir Texto) "
  LOCATE , 7
  PRINT " F3            (Salvar Texto) "
  LOCATE , 7
  PRINT " ESCAPE/ESC  (Sair do Editor) "
  LOCATE 19, 7
  PRINT "____ATALHOS___________________"
  LOCATE , 7
  PRINT " Insert  (Insert CTRL/Y Linha) "
  LOCATE , 7
  PRINT " Qualquer movimento do cursor "
  LOCATE , 7
  PRINT "   Com o SHIFT marca o texto. "
  LOCATE 3, 45
  PRINT "____CONTROL TECLAS____________"
  LOCATE , 45
  PRINT " F1             (Documento 1) "
  LOCATE , 45
  PRINT " F2             (Documento 2) "
  LOCATE , 45
  PRINT " F3   (Area de transferància) "
  LOCATE , 45
  PRINT " Home                (Inicio) "
  LOCATE , 45
  PRINT " End                  (Final) "
  LOCATE , 45
  PRINT " Page Up       (Inicio texto) "
  LOCATE , 45
  PRINT " Page Down      (Final Texto) "
  LOCATE , 45
  PRINT " Left Arrow        (-largura) "
  LOCATE , 45
  PRINT " Right Arrow       (+largura) "
  LOCATE , 45
  PRINT " CTRL/Y       (Deletar Linha) "
  LOCATE 17, 45
  PRINT "____ALT TECLAS________________"
  LOCATE , 45
  PRINT " ALT/H                (Ajuda) "
  LOCATE , 45
  PRINT " ALT/C               (Copiar) "
  LOCATE , 45
  PRINT " ALT/D (Apagar texto marcado) "
  LOCATE , 45
  PRINT " ALT/P                (Colar) "
  LOCATE , 45
  PRINT " ALT/X                 (Sair) "
  T% = GETKEY%
  SCREEN , , 0, 0
END SUB

SUB INFOBAR (D%)
  P$ = SPACE$(80)
  MID$(P$, 7) = "l" + MID$(STR$(CFG(D%).LIN + 1), 2)
  MID$(P$, 27) = STR$(CFG(D%).TOT) + " Linhas"
  MID$(P$, 40) = DOC$(D%)
  MID$(P$, 24) = CHR$(64 + D%)
  MID$(P$, 18) = DOC$(CFG(D%).INS + 4)
  MID$(P$, 13) = "w" + MID$(STR$(CFG(D%).WID), 2)
  LOCATE 1, 1
  MID$(P$, 2) = "c" + MID$(STR$(CFG(D%).CRS), 2)
  COLOR 15, 1
  PRINT P$;
END SUB

SUB INSLINE (D%, S$)
  L% = CFG(D%).LIN
  IF D% = 1 THEN
    FOR T% = 799 TO L% + 1 STEP -1
      D1(T%).L = D1(T% - 1).L
    NEXT
    D1(CFG(1).LIN).L = S$
  END IF
  IF D% = 2 THEN
    FOR T% = 799 TO L% + 1 STEP -1
      D2(T%).L = D2(T% - 1).L
    NEXT
    D2(CFG(2).LIN).L = S$
  END IF
  IF CFG(D%).TOT < 800 THEN CFG(D%).TOT = CFG(D%).TOT + 1
  SHOWDOC D%
END SUB

SUB LINECAT (D%, L%)
  R% = CFG(D%).LIN
  CFG(D%).LIN = L% + 1
  P$ = RTRIM$(GETLINE$(D%))
  C% = CFG(D%).CRS
  CFG(D%).LIN = L%
  T$ = GETLINE$(D%)
  IF R% = L% THEN T$ = LEFT$(T$, C% - 1)
  IF R% <> L% THEN
    T$ = RTRIM$(T$)
    CFG(D%).CRS = LEN(T$) + 1
  END IF
  T$ = T$ + P$
  T% = LEN(T$)
  IF T% < 80 THEN P$ = DELLINE$(D%)
  IF LEN(T$) > 79 THEN
    CFG(D%).LIN = L% + 1
    PUTLINE D%, MID$(T$, 80)
  END IF
  CFG(D%).LIN = L%
  PUTLINE D%, LEFT$(T$, 79)
  SHOWDOC D%
END SUB

SUB LOADDOC (D%)
  DN$ = GETSTR$("Abrir arquivo?", "", 32)
  IF DN$ <> "" THEN DOCOPN DN$, D%
END SUB

FUNCTION MSG% (M$, P$)
  M% = LEN(M$)
  P$ = UCASE$(P$)
  PCOPY 0, 1
  SCREEN , , 1, 1
  IF P$ = "" THEN P$ = CHR$(13) + CHR$(27) + " "
  WHILE INKEY$ <> ""
  WEND
  C% = 37 - (M% + 1) \ 2
  LOCATE 10, C%
  COLOR 9, 1
  LOCATE 11, C%, 0
  PRINT STRING$(M% + 6, CHR$(176))
  LOCATE , C%
  PRINT CHR$(176); CHR$(176);
  COLOR 14, 4
  PRINT " "; M$; " ";
  COLOR 9, 1
  PRINT CHR$(176); CHR$(176)
  LOCATE , C%
  PRINT STRING$(M% + 6, CHR$(176))
  ALARM
  T% = 0
  WHILE INSTR(P$, UCASE$(CHR$(T%))) < 1
    T% = GETKEY%
  WEND
  SCREEN , , 0, 0
  MSG% = INSTR(P$, UCASE$(CHR$(T%)))
END FUNCTION

SUB NOISE
  SOUND 1000, .1
END SUB

SUB ORDERMARK (D%, LA%, LZ%, CA%, CZ%)
  CA% = CFG(D%).MCA
  LA% = CFG(D%).MLA
  CZ% = CFG(D%).MCZ
  LZ% = CFG(D%).MLZ
  LA& = LA%
  LZ& = LZ%
  LA& = LA& * 80 + CA%
  LZ& = LZ& * 80 + CZ%
  IF LA& > LZ& THEN
    SWAP LA%, LZ%
    SWAP CA%, CZ%
  END IF
END SUB

SUB PARMSINIT (D%)
  CFG(D%).INS = 1
  CFG(D%).TOP = 0
  CFG(D%).LIN = 0
  CFG(D%).CRS = 1
  CFG(D%).TOT = 1
  CFG(D%).MLA = 0
  CFG(D%).MCA = 0
  CFG(D%).MLZ = 0
  CFG(D%).MCZ = 0
  CFG(D%).WID = 70
END SUB

SUB PASTETEXT (D%)
  IF CFG(0).TOT = 1 AND RTRIM$(D0(0).L) = "" THEN
    T% = MSG%("Sem texto na µrea de Transferància", "")
    EXIT SUB
  END IF
  L% = CFG(D%).LIN
  R% = L%
  C% = CFG(D%).CRS
  CFG(D%).MCA = 0
  O% = CFG(0).TOT
  I% = O% - 1
  E% = CFG(D%).TOT
  IF (C% > 1) AND (I% = 0) THEN
    T$ = GETLINE$(D%) + SPACE$(80)
    T$ = RTRIM$(LEFT$(T$, C% - 1) + RTRIM$(D0(0).L) + MID$(T$, C%))
    PUTLINE D%, LEFT$(T$, 79)
    IF LEN(T$) > 79 AND L% < 799 THEN
      CFG(D%).LIN = L% + 1
      INSLINE D%, MID$(T$, 80)
      CFG(D%).LIN = L%
    END IF
    NOISE
    EXIT SUB
  END IF
  FOR T% = 799 TO L% STEP -1
    IF T% - 0 >= 0 THEN
      IF D% = 1 THEN D1(T%).L = D1(T% - O%).L
      IF D% = 2 THEN D2(T%).L = D2(T% - O%).L
    END IF
  NEXT
  FOR T% = 0 TO I%
    IF D% = 1 THEN D1(T% + L%).L = D0(T%).L
    IF D% = 2 THEN D2(T% + L%).L = D0(T%).L
  NEXT
  CFG(D%).TOT = E% + O%
  IF E% + O% > 800 THEN CFG(D%).TOT = 800
  NOISE
END SUB

SUB PUTLINE (D%, T$)
  IF D% = 1 THEN D1(CFG(D%).LIN).L = LEFT$(T$ + SPACE$(80), 80)
  IF D% = 2 THEN D2(CFG(D%).LIN).L = LEFT$(T$ + SPACE$(80), 80)
END SUB

SUB SAVEDOC (D%)
  ON ERROR GOTO WHATTHE
  DOC$(3) = "ok"
  L% = CFG(D%).LIN
  CFG(D%).LIN = 0
  T% = CFG(D%).TOT
  P$ = RTRIM$(GETLINE$(D%))
  IF P$ = "" AND T% = 1 THEN
    T% = MSG%("Sem texto", "")
    EXIT SUB
  END IF
  DN$ = DOC$(D%)
  DN$ = GETSTR$("Salvar como?", DN$, 32)
  IF DN$ = "" THEN
    CFG(D%).LIN = L%
    EXIT SUB
  END IF
  DOC$(D%) = DN$
  OPEN DN$ FOR OUTPUT AS #1
  IF DOC$(3) <> "ok" THEN
    T% = MSG%("Erro de arquivo :: Pressione algo", "")
   ELSE
    FOR P% = 0 TO T% - 1
      CFG(D%).LIN = P%
      P$ = RTRIM$(GETLINE$(D%))
      IF P$ = "" THEN P$ = " "
      PRINT #1, P$
    NEXT
    CFG(D%).LIN = L%
    ALARM
    CLOSE 1
  END IF
END SUB

SUB SAVEQRY (D%)
  L% = CFG(D%).LIN
  CFG(D%).LIN = 0
  T% = CFG(D%).TOT
  CFG(D%).LIN = L%
  P$ = RTRIM$(GETLINE$(D%))
  IF P$ = "" AND T% = 1 THEN
    NOISE
    EXIT SUB
  END IF
  T% = MSG%("Salvar documento aberto (Y:N)?", " ny" + CHR$(13))
  IF T% > 2 THEN
    NOISE
    SAVEDOC D%
   ELSE
    ALARM
  END IF
END SUB

SUB SETMARK (D%, SH%)
  IF SH% = 0 THEN EXIT SUB
  L% = CFG(D%).LIN
  C% = CFG(D%).CRS
  L& = L%
  L& = L& * 80 + C%
  A% = CFG(D%).MLA
  A& = A%
  A& = A% * 80 + CFG(D%).MCA
  CFG(D%).MLZ = L%
  CFG(D%).MCZ = C%
END SUB

SUB SHOWDOC (D%)
  L% = CFG(D%).LIN
  T% = CFG(D%).TOP
  COLOR 10, 0
  LOCATE , , 0
  IF T% + 23 < L% THEN
    T% = L% - 23
    CFG(D%).TOP = T%
  END IF
  FOR B% = 0 TO 23
    L% = B% + T%
    IF L% > 799 THEN
      LOCATE 2 + B%, 1
      PRINT SPACE$(80);
     ELSE
      SHOWLINE D%, L%, 2 + B%
    END IF
  NEXT
END SUB

SUB SHOWLINE (D%, L%, S%)
  LA% = 0
  LZ% = 0
  CA% = 0
  CZ% = 0
  IF CFG(D%).MCA <> 0 THEN ORDERMARK D%, LA%, LZ%, CA%, CZ%
  IF D% = 0 THEN T$ = D0(L%).L + SPACE$(80)
  IF D% = 1 THEN T$ = D1(L%).L + SPACE$(80)
  IF D% = 2 THEN T$ = D2(L%).L + SPACE$(80)
  LOCATE S%, 1
  COLOR 10, 0
  IF CA% = 0 OR (LA% > L%) OR (LZ% < L%) THEN
    PRINT LEFT$(T$, 80);
   ELSE
    IF LA% = LZ% THEN
      IF CA% > 1 THEN PRINT LEFT$(T$, CA% - 1);
      COLOR 0, 3
      PRINT MID$(T$, CA%, CZ% - CA% + 1);
      COLOR 10, 0
      PRINT MID$(T$, CZ% + 1, 80 - CZ%);
     ELSE
      IF (L% > LA%) AND (L% < LZ%) THEN
        COLOR 0, 3
        PRINT LEFT$(T$, 80);
       ELSE
        IF L% = LA% THEN
          IF CA% > 1 THEN PRINT LEFT$(T$, CA% - 1);
          COLOR 0, 3
          PRINT MID$(T$, CA%, 81 - CA%);
         ELSE
          COLOR 0, 3
          PRINT LEFT$(T$, CZ%);
          COLOR 10, 0
          PRINT MID$(T$, CZ% + 1, 80 - CZ%);
        END IF
      END IF
    END IF
  END IF
END SUB

