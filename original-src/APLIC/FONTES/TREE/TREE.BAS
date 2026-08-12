DECLARE SUB GetDirs (Path$, Level%)
DECLARE SUB ShowTree ()
DEFINT A-Z
' Dimension Array to Hold Directories and Variable for Number of Dirs
DIM SHARED Path$(1024), DCnt

' Set Active and Visual Pages to 1 and Clear Screen
SCREEN 0, , 1, 1: CLS

' Prompt User for Drive Letter and Prepare Screen
PRINT "Entre com a unidade para pesquisa:";
DO: Drive$ = UCASE$(INKEY$): LOOP UNTIL LEN(Drive$): PRINT Drive$
PRINT "Vasculhando o Drive " + Drive$ + " :"

' Set Frist Path to Root and Directory Count to 1
Path$(1) = Drive$ + ":": DCnt = 1

' Send Output to Page 0 (hide it)
SCREEN 0, , 0, 1

' Start Recursive Directory Scan
GetDirs Path$(1), 1

' Clear Screen and Set OutPut Back to Page 1 and Show Tree
CLS : SCREEN 0, , 1, 1
ShowTree

' Set All Pages to 0 and End
SCREEN 0, , 0, 0
END

SUB GetDirs (Path$, Level)
' Clear Screen, Display Sub Directories in Path$, Find out Last Line
CLS : FILES RTRIM$(Path$) + "\*.": LastLin = CSRLIN - 3

' Start Scanning Each Line of the Screen for Directory Entries
FOR Lin = 1 TO LastLin
  FOR Col = 0 TO 3   ' Start with Column Offset of 0
    D$ = ""          ' Clear Temp Character Variable
    DEF SEG = &HB800 ' Set Default Segment to Video Memory
    ' Read One Entry (17 Characters) From Video Memory
    FOR Char = 0 TO 34 STEP 2
      D$ = D$ + CHR$(PEEK(Lin * 160 + Col * 36 + Char))
    NEXT
    DEF SEG ' Set Default Segement Back to QB Data
    ' If Entry is a Sub Directory
    IF INSTR(D$, "<DIR>") AND INSTR(D$, ".") = 0 THEN
      ' Update Count on Visible Page
      DCnt = DCnt + 1: SCREEN 0, , 1, 1: LOCATE 2, 19
      PRINT LTRIM$(STR$(DCnt)): SCREEN 0, , 0, 1
      ' Add the Parent Path to the Name
      P$ = RTRIM$(Path$) + "\" + RTRIM$(LEFT$(D$, INSTR(D$, " ")))
      ' Store it to the Array
      Path$(DCnt) = P$
      ' Do a Recursive Search of That Sub for Subs
      GetDirs P$, Level + 1
      ' Find Last \ in Directory Name
      DO
        W = INSTR(W + 1, P$, "\"): IF W THEN P = W
      LOOP WHILE W
      ' Parse Out the Parent Directory
      P$ = LEFT$(P$, P - 1)
      ' Clear Screen, Re-Display Parent Directory, and Get Last Line
      CLS : FILES P$ + "\*.": LastLin = CSRLIN - 3
    END IF
  NEXT
NEXT
END SUB

SUB ShowTree
CLS : S = 1: Top = 1: IF DCnt < 22 THEN Max = DCnt - 1 ELSE Max = 22
Refresh:
OldN = 0: N = 0
FOR I = Top TO Top + Max
  P$ = Path$(I): N = 0: P = 0
  DO
    W = INSTR(W + 1, P$, "\"): IF W THEN P = W: N = N + 1
  LOOP WHILE W
  P2$ = Path$(I + 1): N2 = 0: P2 = 0
  DO
    W = INSTR(W + 1, P2$, "\"): IF W THEN P2 = W: N2 = N2 + 1
  LOOP WHILE W
  Nof$ = LEFT$(RIGHT$(P$, LEN(P$) - P) + SPACE$(20), 20)
  IF INSTR(Nof$, ":") THEN
    LOCATE I, S: PRINT LEFT$("\" + SPACE$(20), 20)
  ELSE
    T$ = "": FOR J = 1 TO N - 1: T$ = "³  " + T$: NEXT
    IF N2 < N THEN T$ = T$ + "À" ELSE T$ = T$ + "Ã"
    LOCATE I - Top + 1, S: PRINT T$ + "ÄÄ";
    PRINT Nof$
  END IF
NEXT
DO: K$ = INKEY$: LOOP UNTIL LEN(K$)
IF LEN(K$) = 1 THEN K = ASC(K$) ELSE K = -ASC(RIGHT$(K$, 1))
SELECT CASE K
  CASE 27
    CLS : EXIT SUB
  CASE -80
    Top = Top + 1
  CASE -72
    Top = Top - 1
  CASE -73
    Top = Top - 19
  CASE -81
    Top = Top + 19
END SELECT
IF Top < 1 THEN Top = 1
IF Top + Max > DCnt THEN Top = DCnt - Max
GOTO Refresh

END SUB

