'      Release:  GERMS Version 1.0 by William Yu  (03-30-97)
'       Status:  100% Public Domain, please give credit where due.
'      Contact:  e-mail: voxel@freenet.edmonton.ab.ca
DEFINT A-Z
DECLARE SUB DrawTitle (Pill%(), PillColours%(), Clock%())
DECLARE SUB InitJoystick (JoyStick() AS ANY)
DECLARE FUNCTION MatchVertical% (Grid%(), SX%, SY%)
DECLARE FUNCTION MatchHorizontal% (Grid%(), SX%, SY%)
DECLARE SUB RandCycle (Cycle%)
DECLARE SUB SetColours (PillColours%())
DECLARE SUB SetBoundary (StartX%, StartY%, EndX%, EndY%, BoundY%, BoundXMin%, BoundXMax%)
DECLARE SUB InitPills (Pill%(), PillColours%())
DECLARE SUB DrawBackGround ()
DECLARE SUB InitGrid (Grid%(), GridJoin%())
DECLARE SUB RandGerms (Grid%(), PillColours%(), OrigX%, OrigY%, Germs%, MaxHeight%)
DECLARE SUB Delay (Seconds!)
DECLARE SUB CheckForSuspension (Grid%(), GridJoin%(), PillColours%(), OrigX%, OrigY%)
DECLARE SUB CheckForMatch (Grid%(), GridJoin%(), PillColours%(), OrigX%, OrigY%, Match%)
DECLARE SUB GetPill (PillColours%(), Clock%(), Colour1%, Colour2%)
DECLARE SUB DrawPillVert (X%, Y%, PCol1%, PCol2%, RefCol1%, RefCol2%)
DECLARE SUB BoxIt (StartX%, StartY%, EndX%, EndY%, Colour%)
DECLARE SUB DrawPillHorz (X%, Y%, PCol1%, PCol2%, RefCol1%, RefCol2%)

OPTION BASE 1

CONST False = 0
CONST True = NOT False


CONST ScreenMode = 7
CONST XMax = 320
CONST YMax = 200
                     
                     

CONST UseJoy = False
                     

CONST SpeedDown = 32
CONST Drop = -80
CONST Rotate = -72
CONST Right = -77
CONST Left = -75

CONST Cells = 4
CONST Height = 20

CONST MatchIt = 4
                      
                      

CONST Colours = 3

CONST GermScore = 125
CONST DefaultScore = 125
CONST TBonus = 1000
                         

Level = 1
Speed! = 2
SpeedInc! = .1


CONST CellLength = 26
CONST CellHeight = 9

TYPE JoyType
  InitialX AS INTEGER
  InitialY AS INTEGER
  Up AS INTEGER
  Down AS INTEGER
  Left AS INTEGER
  Right AS INTEGER
END TYPE

DIM JoyStick(2) AS JoyType

DIM Pill(81 * 2 * 9) AS INTEGER
DIM Grid(Cells * 2, Height) AS INTEGER
DIM GridJoin(Cells * 2, Height) AS INTEGER
DIM PillColours(Colours, 2)
DIM Clock(4), ClockNext(4)

DIM SHARED Germs
DIM SHARED TotalScore AS LONG

IF UseJoy THEN InitJoystick JoyStick()

SCREEN ScreenMode
                     

InitGrid Grid(), GridJoin()
SetColours PillColours()
SetBoundary StartX, StartY, EndX, EndY, BoundY, BoundXMin, BoundXMax

FOR I = 1 TO 15
  PALETTE I, 0
NEXT I

InitPills Pill(), PillColours()

DrawBackGround
'DrawTitle Pill(), PillColours(), Clock() 

BoxIt StartX, StartY, EndX, EndY - 1, 0
BoxIt EndX + 15, StartY, EndX + 97, StartY + 45, 0
BoxIt EndX + 15, StartY + 65, EndX + 97, StartY + 160, 0

Horizontal = True     '' Initial pill position
OrigX = StartX        '' Record all initial positions of grid
OrigY = StartY        ''   for later referencing
GridX = Cells         '' Center pill on the grid
GridY = 1             '' Always start at the top (of course!)

GetPill PillColours(), Clock(), Colour1, Colour2         '' Use current pill
GetPill PillColours(), ClockNext(), NColour1, NColour2   '' Record next pill

Germs = Level * 3     '' Algorithm to calculate the # of germs on the
                      '' playing field at each level

'' Randomly place the germs on the grid
RandGerms Grid(), PillColours(), OrigX, OrigY, Germs, Level + 1

RandCycle Cycle      '' Get random position of pill (Horizontal only)
RandCycle NextCycle

StartX = StartX + (GridX - 1) * CellLength / 2  '' Starting XCoord of Pill

PUT (StartX, StartY), Pill(Clock(Cycle)), PSET  '' Put current on playing field
PUT (257, 37), Pill(ClockNext(Cycle)), PSET     '' Put next for user to see

Done = False
DropIt = False
Clock = 1       ' 1,3 = Horizontal  2,4 = Vertical
TotalScore = 0

LOCATE 3, 33: COLOR 15: PRINT "PROX"
LOCATE 11, 32: PRINT "PONTOS"
LOCATE 13, 31: COLOR 15: PRINT USING "###,###"; TotalScore
LOCATE 15, 32: COLOR 10: PRINT "GERMS"
LOCATE 17, 33: COLOR 11: PRINT Germs
LOCATE 19, 32: COLOR 15: PRINT "NIVEL"
LOCATE 21, 33: PRINT Level

' Remove for all modes except 7,8,9
FOR I = 1 TO 15
  PALETTE I, I       '' Unhide the screen
NEXT I

DropDelay! = TIMER + 1 / (Speed! + 1)  '' Algorithm for delay in descending

IF UseJoy THEN
  WHILE STRIG(0) OR STRIG(1): WEND
END IF

DO
  DO
    IF UseJoy THEN
      A = STICK(0)
      B = STICK(1)
      A$ = INKEY$
      IF A$ = CHR$(27) THEN Char = 27
      IF A = JoyStick(1).Left OR A = JoyStick(2).Left THEN Char = Left: A$ = " "
      IF A = JoyStick(1).Right OR A = JoyStick(2).Right THEN Char = Right: A$ = " "
      IF B = JoyStick(1).Down OR B = JoyStick(2).Down THEN Char = SpeedDown: A$ = " "
      IF STRIG(0) THEN GOSUB RotatePill
    ELSE
      A$ = INKEY$    '' Get input from user
    END IF
    GOSUB CheckDrop
    IF DropIt THEN A$ = ""
  LOOP UNTIL LEN(A$)
 
  IF NOT UseJoy THEN
    IF LEN(A$) = 2 THEN Char = -ASC(RIGHT$(A$, 1)) ELSE Char = ASC(A$)
  END IF
 
  SELECT CASE Char
    CASE SpeedDown
      DropDelay! = TIMER
    CASE Drop
      DropIt = True
    CASE Rotate
      GOSUB RotatePill
    CASE Right
      PUT (StartX, StartY), Pill(Clock(Cycle))
      IF Horizontal THEN CheckX = StartX + CellLength / 2 ELSE CheckX = StartX
      IF CheckX <= BoundXMax THEN
        '' Check for occupied space in grid.
        IF Horizontal THEN
          IF (Grid(GridX + 2, GridY) = 0) THEN
            StartX = StartX + (CellLength / 2)
            GridX = GridX + 1
          END IF
        ELSE
          IF (Grid(GridX + 1, GridY) = 0) AND (Grid(GridX + 1, GridY + 1) = 0) THEN
            StartX = StartX + (CellLength / 2)
            GridX = GridX + 1
          END IF
        END IF
      END IF
      PUT (StartX, StartY), Pill(Clock(Cycle)), PSET
    CASE Left
      PUT (StartX, StartY), Pill(Clock(Cycle))
      IF StartX > BoundXMin THEN
        ' Check for occupied space in grid.
        IF Horizontal THEN
          IF (Grid(GridX - 1, GridY) = 0) THEN
            StartX = StartX - (CellLength / 2)
            GridX = GridX - 1
          END IF
        ELSE
          IF (Grid(GridX - 1, GridY) = 0) AND (Grid(GridX - 1, GridY + 1) = 0) THEN
            StartX = StartX - (CellLength / 2)
            GridX = GridX - 1
          END IF
        END IF
      END IF
      PUT (StartX, StartY), Pill(Clock(Cycle)), PSET
  END SELECT
  DO: LOOP UNTIL INKEY$ = ""
LOOP UNTIL Char = 27        '' loop until user pressed ESC

END

RotatePill:
      PUT (StartX, StartY), Pill(Clock(Cycle))
      Horizontal = NOT Horizontal       '' Rotate Horizontal <-> Vertical
      IF Horizontal = True THEN
        IF (StartX <= BoundXMax) THEN   '' Can't rotate if on the right-most edge
          IF Grid(GridX + 1, GridY) = 0 THEN
            Cycle = Cycle + 1
            IF Cycle = 5 THEN Cycle = 1
          ELSE                          '' Can't rotate if something is beside it.
            Horizontal = False
          END IF
        ELSE
          Horizontal = False
        END IF
      ELSE
        IF StartY <= BoundY THEN        ' Can't rotate if on last line
          IF Grid(GridX, GridY + 1) = 0 THEN
            Cycle = Cycle + 1
            IF Cycle = 5 THEN Cycle = 1
          ELSE                          ' Can't rotate if something is beneath it.
            Horizontal = True
          END IF
        ELSE
          Horizontal = True
        END IF
      END IF
      PUT (StartX, StartY), Pill(Clock(Cycle)), PSET
      IF UseJoy THEN
        WHILE STRIG(0) OR STRIG(1)
          GOSUB CheckDrop
        WEND
      END IF
RETURN

CheckDrop:
    IF TIMER >= DropDelay! OR DropIt THEN   '' Move pill down
      PUT (StartX, StartY), Pill(Clock(Cycle))
      DropDelay! = TIMER + 1 / (Speed! + 1)
      IF Horizontal THEN CheckY = StartY ELSE CheckY = StartY + CellHeight
      IF CheckY <= BoundY THEN   '' Make sure pill hasn't reached the bottom
        IF Horizontal THEN       '' Check for Horizontal Pill position
          '' Make sure there isn't anything underneath the pill
          IF (Grid(GridX, GridY + 1) = 0) AND (Grid(GridX + 1, GridY + 1) = 0) THEN
            StartY = StartY + CellHeight
            GridY = GridY + 1    '' Move pill down one
          ELSE
            Done = True          '' Can't move any farther down
          END IF
        ELSE                     '' Check for vertical pill position
          '' Make sure there isn't anything underneath the pill
          IF (Grid(GridX, GridY + 1) = 0) AND (Grid(GridX, GridY + 2) = 0) THEN
            StartY = StartY + CellHeight
            GridY = GridY + 1    '' Move pill down one
          ELSE
            Done = True          '' Can't move down any farther
          END IF
        END IF
      ELSE
        Done = True              '' Pill has reached the bottom
      END IF
      IF Done THEN
        PUT (StartX, StartY), Pill(Clock(Cycle)), PSET
        StartY = OrigY
        StartX = OrigX
        IF Horizontal THEN
          SELECT CASE Cycle
            CASE 1
              Grid(GridX, GridY) = Colour1
              Grid(GridX + 1, GridY) = Colour2
              GridJoin(GridX, GridY) = 1      '' Left
              GridJoin(GridX + 1, GridY) = 2  '' Right
            CASE 3
              Grid(GridX, GridY) = Colour2
              Grid(GridX + 1, GridY) = Colour1
              GridJoin(GridX, GridY) = 1
              GridJoin(GridX + 1, GridY) = 2
          END SELECT
        ELSE
          SELECT CASE Cycle
            CASE 2
              Grid(GridX, GridY) = Colour1
              Grid(GridX, GridY + 1) = Colour2
              GridJoin(GridX, GridY) = 3      '' Up
              GridJoin(GridX, GridY + 1) = 4  '' Down
            CASE 4
              Grid(GridX, GridY) = Colour2
              Grid(GridX, GridY + 1) = Colour1
              GridJoin(GridX, GridY) = 3
              GridJoin(GridX, GridY + 1) = 4
          END SELECT
        END IF
        DO
          CheckForMatch Grid(), GridJoin(), PillColours(), OrigX, OrigY, Match
          IF Match THEN
            CheckForSuspension Grid(), GridJoin(), PillColours(), OrigX, OrigY
          END IF
        LOOP UNTIL Match = False
        LOCATE 13, 31: COLOR 15: PRINT USING "###,###"; TotalScore
        LOCATE 17, 33: COLOR 11: PRINT Germs
        GridX = Cells: GridY = 1
        IF Grid(GridX, GridY) <> 0 OR Grid(GridX, GridY) <> 0 THEN END
        IF Germs = 0 THEN
          Bonus = TBonus              '' Total obtainable bonus
          FOR I = 1 TO Height
            FOR J = 1 TO Cells * 2    '' Minus 50 for each cell left on playing field
              IF Grid(J, I) <> 0 THEN
                Bonus = Bonus - 50
                SX = OrigX + ((J - 1) * CellLength / 2)
                SY = OrigY + (CellHeight * (I - 1))
                LINE (SX, SY)-(SX + CellLength / 2 - 1, SY + CellHeight - 1), 0, BF
                Delay .001
              END IF
              Grid(J, I) = 0
              GridJoin(J, I) = 0
            NEXT J
          NEXT I
          IF Bonus < 0 THEN Bonus = 0
          TotalScore = TotalScore + Bonus
          LINE (125, 90)-(193, 125), 0, BF
          LINE (125, 90)-(193, 125), 12, B
          LOCATE 13, 18: PRINT "Bonus:"
          LOCATE 15, 18: PRINT Bonus
          IF UseJoy THEN
            WHILE NOT STRIG(0) AND NOT STRIG(1): WEND
          ELSE
            A$ = INPUT$(1)
          END IF
          Level = Level + 1
          Lev = Level
          IF Level > 14 THEN
            Germs = Level * 3 + (Level - 15)
            Lev = 14
          ELSE
            Germs = Level * 3
          END IF
          IF Level = 33 THEN END
          Speed! = Speed! + SpeedInc!
          BoxIt StartX, StartY, EndX, EndY - 1, 0
          RandGerms Grid(), PillColours(), OrigX, OrigY, Germs, Lev + 1
          LOCATE 13, 31: COLOR 15: PRINT USING "###,###"; TotalScore
          LOCATE 17, 33: COLOR 11: PRINT Germs
          LOCATE 21, 33: PRINT Level
        END IF
        StartX = StartX + (GridX - 1) * CellLength / 2
        Done = False
        DropIt = False
        FOR I = 1 TO 4
          Clock(I) = ClockNext(I)   '' Use next pill as current pill
        NEXT I
        Colour1 = NColour1
        Colour2 = NColour2
        Cycle = NextCycle
        RandCycle NextCycle  '' Get next random position of pill
        GetPill PillColours(), ClockNext(), NColour1, NColour2
        PUT (257, 37), Pill(ClockNext(NextCycle)), PSET
        Horizontal = True    '' Always on the horizontal position
      END IF  '' Done
      PUT (StartX, StartY), Pill(Clock(Cycle)), PSET
    END IF    '' DropIt
RETURN

SUB BoxIt (StartX, StartY, EndX, EndY, Colour)
'*****************************************************************
' Function: Draw a simple faded looking box from StartX, StartY
'           to EndX, EndY.  Colour, being the colour to fill the
'           box with.
'*****************************************************************

LINE (StartX - 1, StartY - 1)-(EndX + 1, EndY + 1), 15, B
LINE (StartX - 2, StartY - 2)-(EndX + 2, EndY + 2), 7, B
LINE (StartX - 3, StartY - 3)-(EndX + 3, EndY + 3), 8, B
LINE (StartX, StartY)-(EndX, EndY), Colour, BF

END SUB

SUB CheckForMatch (Grid(), GridJoin(), PillColours(), OrigX, OrigY, Match)
'*************************************************************************
' Function: Scans through the entire grid (twice in worst case scenario),
'           looking for a match.  Will ONLY look up ONE match per call.
'   Output: Eliminates the cells on the screen and the Grid() array
'*************************************************************************


Match = False
Score = 0

IF MatchHorizontal(Grid(), SX, SY) THEN
  StartY = OrigY + (CellHeight * (SY - 1))
  StartX = OrigX + ((SX - 1) * CellLength / 2)

  ''----- Spiffy little routine to acknowledge a match -----
  FOR C = 1 TO 2
    FOR J = 0 TO MatchIt
      IF J < MatchIt AND C = 1 THEN
        IF Grid(SX + J, SY) < 0 THEN TotalScore = TotalScore + GermScore: Germs = Germs - 1
      END IF
      FOR I = 1 TO MatchIt
        IF C = 1 THEN
          LINE (StartX + J, StartY + J)-(StartX + I * CellLength / 2 - J - 1, StartY + CellHeight - 1 - J), 15, B
        ELSE
          LINE (StartX + J, StartY + J)-(StartX + I * CellLength / 2 - J - 1, StartY + CellHeight - 1 - J), 0, B
        END IF
      NEXT I
      IF J = 2 AND C = 2 THEN Delay .01
    NEXT J
    Delay .01
  NEXT C
  ''----- End Spiffy routine to acknowledge a match -----

  LINE (StartX, StartY)-(StartX + MatchIt * CellLength / 2 - 1, StartY + CellHeight - 1), 0, BF
  TotalScore = TotalScore + DefaultScore
  FOR I = 0 TO MatchIt - 1
    A = GridJoin(SX + I, SY)
    GridJoin(SX + I, SY) = 0    ' Unjoin any cells that were previously joined
    SELECT CASE A
      CASE 1 '' Left
        '' In this case, the pill (L|R) becomes a circle (R)
        ''                        ^^ deleted part
        IF I = MatchIt - 1 THEN
          StartX = OrigX + ((SX + 3) * CellLength / 2 + 3)
          StartY = OrigY + (CellHeight * (SY - 1)) + 4
          CIRCLE (StartX - 3, StartY), 4, 0, 1.7, 4.7, 2.2
          CIRCLE (StartX - 2, StartY), 4, 0, 1.7, 4.6, 2.2
          GridJoin(SX + I + 1, SY) = 0
        END IF
      CASE 2 '' Right
        '' In this case, the pill (L|R) becomes a circle (L)
        ''                           ^^ deleted part
        IF I = 0 THEN
          StartX = OrigX + ((SX - 1) * CellLength / 2 - 3)
          StartY = OrigY + (CellHeight * (SY - 1)) + 4
          CIRCLE (StartX + 2, StartY), 4, 0, 4.5, 1.4, 2.2
          CIRCLE (StartX + 1, StartY), 4, 0, 4.5, 1.4, 2.2
          GridJoin(SX + I - 1, SY) = 0
        END IF
      CASE 3 '' Up
        '' In this case, the pill (U/D) becomes a circle (D)
        ''                        ^^ deleted part
        StartX = OrigX + ((SX + I - 1) * CellLength / 2 + 6)
        StartY = OrigY + (CellHeight * SY) + 1
        CIRCLE (StartX, StartY), 6, 0, 0, 3.3, 1 / 3
        GridJoin(SX + I, SY + 1) = 0
      CASE 4 '' Down
        '' In this case, the pill (U/D) becomes a circle (U)
        ''                           ^^ deleted part
        StartX = OrigX + ((SX + I - 1) * CellLength / 2 + 6)
        StartY = OrigY + (CellHeight * (SY - 1)) - 2
        CIRCLE (StartX, StartY), 6, 0, 3, 0, 1 / 3
        GridJoin(SX + I, SY - 1) = 0
    END SELECT
    Grid(SX + I, SY) = 0
  NEXT I
  Match = True
ELSE
  IF MatchVertical(Grid(), SX, SY) THEN
    StartY = OrigY + (CellHeight * (SY - 1))
    StartX = OrigX + ((SX - 1) * CellLength / 2)
   
    ''----- Spiffy little routine to acknowledge a match -----
    FOR C = 1 TO 2
      IF C = 2 THEN LINE (StartX, StartY)-(StartX + CellLength / 2 - 1, StartY + MatchIt * CellHeight - 1), 15, BF
      FOR J = 0 TO MatchIt
        IF J < MatchIt AND C = 1 THEN
          IF Grid(SX, SY + J) < 0 THEN TotalScore = TotalScore + GermScore: Germs = Germs - 1
        END IF
        FOR I = 1 TO MatchIt
          IF C = 1 THEN
            LINE (StartX + J, StartY + J)-(StartX + CellLength / 2 - J - 1, StartY + I * CellHeight - J - 1), 15, B
          ELSE
            LINE (StartX + J, StartY + J)-(StartX + CellLength / 2 - J - 1, StartY + I * CellHeight - 1 - J), 0, B
          END IF
        NEXT I
      NEXT J
     Delay .01
    NEXT C
    ''----- End Spiffy routine to acknowledge a match -----

    LINE (StartX, StartY)-(StartX + CellLength / 2 - 1, StartY + MatchIt * CellHeight - 1), 0, BF
    TotalScore = TotalScore + DefaultScore
    FOR I = 0 TO MatchIt - 1
      Grid(SX, SY + I) = 0
      A = GridJoin(SX, SY + I)
      GridJoin(SX, SY + I) = 0
      SELECT CASE A
        CASE 1 '' Left
          StartX = OrigX + (SX * CellLength / 2 + 3)
          StartY = OrigY + (CellHeight * (SY + I - 1)) + 4
          CIRCLE (StartX - 3, StartY), 4, 0, 1.7, 4.7, 2.2
          CIRCLE (StartX - 2, StartY), 4, 0, 1.7, 4.6, 2.2
          GridJoin(SX + 1, SY + I) = 0
        CASE 2 '' Right
          StartX = OrigX + ((SX - 1) * CellLength / 2 - 3)
          StartY = OrigY + (CellHeight * (SY + I - 1)) + 4
          CIRCLE (StartX + 2, StartY), 4, 0, 4.5, 1.4, 2.2
          CIRCLE (StartX + 1, StartY), 4, 0, 4.5, 1.4, 2.2
          GridJoin(SX - 1, SY + I) = 0
        CASE 3 '' Up
          IF I = MatchIt - 1 THEN
            StartX = OrigX + ((SX - 1) * CellLength / 2 + 6)
            StartY = OrigY + (CellHeight * (SY + I)) + 1
            CIRCLE (StartX, StartY), 6, 0, 0, 3.3, 1 / 3
            GridJoin(SX, SY + I + 1) = 0
          END IF
        CASE 4 '' Down
          IF I = 0 THEN
            StartX = OrigX + ((SX - 1) * CellLength / 2 + 6)
            StartY = OrigY + (CellHeight * (SY - 1)) - 2
            CIRCLE (StartX, StartY), 6, 0, 3, 0, 1 / 3
            GridJoin(SX, SY - 1) = 0
          END IF
      END SELECT
    NEXT I
    Match = True
  END IF
END IF

END SUB

SUB CheckForSuspension (Grid(), GridJoin(), PillColours(), OrigX, OrigY)
'**********************************************************************
'   Function: This procedure is called after CheckForMatch.
'             It looks for any suspended pills which should be brought
'             down due to the elimination of any matches.
'             Any suspended pills are brought down one at a time, until
'             it hits a surface.
'Author NOTE: Up and Right cases are not required because they can
'             never be suspended by the SUB CheckForMatch.
'             They can only form half a pill, in which case join=0 would
'             accounted for that.
'             ie.  (L|R)   (U/D)
'                     ^^   ^^
'                     These would form a half a pill.
'**********************************************************************

REDIM Cell(81)

FOR Y = Height - 1 TO 1 STEP -1     '' From bottom up
  X = 0
  WHILE X < Cells * 2
    X = X + 1
    IF (Grid(X, Y) > 0) AND (Grid(X, Y + 1) = 0) THEN
       SELECT CASE GridJoin(X, Y)
         CASE 0
           REDIM Cell(1 TO 81)
           StartX = OrigX + ((X - 1) * CellLength / 2)
           StartY = OrigY + (CellHeight * (Y - 1))
           GET (StartX, StartY)-(StartX + CellLength / 2 - 1, StartY + CellHeight - 1), Cell
           A = Grid(X, Y)
           FOR I = Y + 1 TO Height
             IF Grid(X, I) = 0 THEN
               PUT (StartX, StartY), Cell
               StartY = OrigY + (CellHeight * (I - 1))
               PUT (StartX, StartY), Cell, PSET
               Grid(X, I - 1) = 0
               Grid(X, I) = A
             ELSE
               EXIT FOR
             END IF
           NEXT I
         CASE 1  ' Left
           REDIM Cell(1 TO 81)
           StartX = OrigX + ((X - 1) * CellLength / 2)
           StartY = OrigY + (CellHeight * (Y - 1))
           GET (StartX, StartY)-(StartX + CellLength - 1, StartY + CellHeight - 1), Cell
           A = Grid(X, Y)
           B = Grid(X + 1, Y)
           FOR I = Y + 1 TO Height
             IF (Grid(X, I) = 0) AND (Grid(X + 1, I) = 0) THEN
               PUT (StartX, StartY), Cell
               StartY = OrigY + (CellHeight * (I - 1))
               PUT (StartX, StartY), Cell, PSET
               Grid(X, I - 1) = 0
               Grid(X + 1, I - 1) = 0
               GridJoin(X, I - 1) = 0
               GridJoin(X + 1, I - 1) = 0
               Grid(X, I) = A
               Grid(X + 1, I) = B
               GridJoin(X, I) = 1
               GridJoin(X + 1, I) = 2
             ELSE
               EXIT FOR
             END IF
           NEXT I
           X = X + 1
         CASE 4  ' Down
           REDIM Cell(1 TO 81)
           StartX = OrigX + ((X - 1) * CellLength / 2)
           StartY = OrigY + (CellHeight * (Y - 2))
           GET (StartX, StartY)-(StartX + CellLength / 2 - 1, StartY + 2 * CellHeight - 1), Cell
           A = Grid(X, Y)
           B = Grid(X, Y - 1)
           FOR I = Y + 1 TO Height
             IF Grid(X, I) = 0 THEN
               PUT (StartX, StartY), Cell
               StartY = OrigY + (CellHeight * (I - 2))
               PUT (StartX, StartY), Cell, PSET
               Grid(X, I - 2) = 0
               GridJoin(X, I - 2) = 0
               Grid(X, I) = A
               Grid(X, I - 1) = B
               GridJoin(X, I - 1) = 3
               GridJoin(X, I) = 4
             ELSE
               EXIT FOR
             END IF
           NEXT I
       END SELECT
    END IF
  WEND
NEXT Y

END SUB

SUB Delay (Seconds!)

T! = TIMER
DO
LOOP UNTIL ABS(TIMER - T!) >= Seconds!

END SUB

SUB DrawBackGround

CLS
Initial = 0
LINE (Initial, 0)-(XMax, YMax), 4, BF
FOR X = Initial TO XMax
  LINE (X, X - Initial)-(Initial, X - Initial), 1
  LINE (XMax - X + Initial, X - Initial)-(XMax, X - Initial), 2
NEXT X

END SUB

SUB DrawPillHorz (X, Y, PCol1, PCol2, RefCol1, RefCol2)
'*********************************************************
' Function: This procedure draws the pills horizontally
'*********************************************************

Radius = 4

CIRCLE (X + CellLength - (2 * Radius) + 1, Y), Radius, PCol2, 4.5, 1.7, 1.5
CIRCLE (X, Y), Radius, PCol1, 1.7, 4.9, 1.5
LINE (X, Y - Radius)-(X + CellLength / 2 - Radius, Y - Radius), PCol1
LINE (X + CellLength / 2 - Radius + 1, Y - Radius)-(X + CellLength - 2 * Radius, Y - Radius), PCol2
LINE (X, Y + Radius)-(X + CellLength / 2 - Radius, Y + Radius), PCol1
LINE (X + CellLength / 2 - Radius + 1, Y + Radius)-(X + CellLength - 2 * Radius, Y + Radius), PCol2
IF PCol2 <> PCol1 THEN
  LINE (X + CellLength / 2 - Radius, Y + Radius)-(X + CellLength / 2 - Radius, Y - Radius), PCol1
  LINE (X + CellLength / 2 - Radius + 1, Y + Radius)-(X + 1 + CellLength / 2 - Radius, Y - Radius), PCol2
  PAINT (X + Radius, Y + 2), PCol1
  PAINT (X + CellLength / 2, Y + 2), PCol2
ELSE
  PAINT (X + 4, Y + 2), PCol1
END IF
CIRCLE (X + 2, Y + 1), Radius, RefCol1, 1.2, 3, 1.5
CIRCLE (X + CellLength - (2 * Radius) - 1, Y - 1), Radius, RefCol2, 4.4, 0, 1.5

END SUB

SUB DrawPillVert (X, Y, PCol1, PCol2, RefCol1, RefCol2)
'*********************************************************
' Function: This procedure draws the pills vertically
'*********************************************************

Radius = 6

CIRCLE (X + Radius / 2, Y - 1), Radius, PCol1, 0, 3.3, 1 / 2
CIRCLE (X + Radius / 2, Y + 10), Radius, PCol2, 3, 0, 1 / 2
LINE (X - Radius / 2, Y + 1)-(X - 3, Y + 4), PCol1
LINE (X - Radius / 2, Y + 5)-(X - 3, Y + CellHeight - 1), PCol2
LINE (X + 9, Y)-(X + 9, Y + 4), PCol1
LINE (X + 9, Y + 5)-(X + 9, Y + 9), PCol2
IF PCol1 <> PCol2 THEN
  LINE (X - Radius / 2, Y + 4)-(X + 9, Y + 4), PCol1
  LINE (X - Radius / 2, Y + 5)-(X + 9, Y + 5), PCol2
  PAINT (X + 2, Y + 2), PCol1
  PAINT (X + 2, Y + 7), PCol2
ELSE
  PAINT (X + 2, Y + 2), PCol1
END IF
CIRCLE (X + Radius / 2 + 1, Y), 6, RefCol1, 2.1, 3.2, 1 / 2
CIRCLE (X + Radius / 2 - 1, Y + 9), 6, RefCol2, 5.3, 0, 1 / 2

END SUB

SUB DrawTitle (Pill(), PillColours(), Clock())

CurrPill = 16 * 80 + 1
Clock(1) = CurrPill
Clock(2) = CurrPill + 80
Clock(3) = CurrPill
Clock(4) = CurrPill + 80

' G
PUT (10, YMax - CellHeight * 2), Pill(Clock(1)), PSET
PUT (10 + CellLength, YMax - CellHeight * 2), Pill(Clock(1)), PSET
PUT (10 + 2 * CellLength, YMax - CellHeight * 2), Pill(Clock(1)), PSET
PUT (10, YMax - CellHeight * 3), Pill(Clock(2)), PSET
PUT (10, YMax - CellHeight * 5), Pill(Clock(4)), PSET
PUT (10 + CellLength * 3, YMax - CellHeight * 3), Pill(Clock(2)), PSET
PUT (10 + CellLength * 3, YMax - CellHeight * 5), Pill(Clock(4)), PSET
PUT (10 + 2 * CellLength, YMax - CellHeight * 5), Pill(Clock(1)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 5), Pill(Clock(4)), PSET

' E
PUT (10, YMax - CellHeight * 7), Pill(Clock(1)), PSET
PUT (10 + CellLength, YMax - CellHeight * 7), Pill(Clock(1)), PSET
PUT (10 + 2 * CellLength, YMax - CellHeight * 7), Pill(Clock(1)), PSET
PUT (10, YMax - CellHeight * 8), Pill(Clock(2)), PSET
PUT (10, YMax - CellHeight * 10), Pill(Clock(4)), PSET
PUT (10 + CellLength * 3, YMax - CellHeight * 8), Pill(Clock(2)), PSET
PUT (10 + CellLength * 3, YMax - CellHeight * 10), Pill(Clock(4)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 9), Pill(Clock(4)), PSET

' R
PUT (10 + CellLength / 2, YMax - CellHeight * 12), Pill(Clock(1)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 12), Pill(Clock(1)), PSET
PUT (10 + 2 * CellLength + CellLength / 2, YMax - CellHeight * 12), Pill(Clock(1)), PSET
PUT (10, YMax - CellHeight * 13), Pill(Clock(2)), PSET
PUT (10, YMax - CellHeight * 15), Pill(Clock(4)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 9), Pill(Clock(4)), PSET
PUT (10 + 2 * CellLength + CellLength / 2, YMax - CellHeight * 12), Pill(Clock(1)), PSET
PUT (10, YMax - CellHeight * 16), Pill(Clock(1)), PSET
PUT (10 + CellLength - CellLength / 2, YMax - CellHeight * 16), Pill(Clock(1)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 15), Pill(Clock(2)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 14), Pill(Clock(2)), PSET
PUT (10 + 2 * CellLength, YMax - CellHeight * 15), Pill(Clock(1)), PSET
PUT (10 + 3 * CellLength, YMax - CellHeight * 16), Pill(Clock(2)), PSET

' M
PUT (10 + CellLength / 2, YMax - CellHeight * 18), Pill(Clock(1)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 18), Pill(Clock(1)), PSET
PUT (10 + 2 * CellLength + CellLength / 2, YMax - CellHeight * 18), Pill(Clock(1)), PSET
PUT (10, YMax - CellHeight * 19), Pill(Clock(2)), PSET
PUT (10, YMax - CellHeight * 22), Pill(Clock(4)), PSET
PUT (10, YMax - CellHeight * 20), Pill(Clock(1)), PSET
PUT (10 + CellLength - CellLength / 2, YMax - CellHeight * 20), Pill(Clock(1)), PSET
PUT (10 + CellLength / 2, YMax - CellHeight * 22), Pill(Clock(1)), PSET
PUT (10 + CellLength + CellLength / 2, YMax - CellHeight * 22), Pill(Clock(1)), PSET
PUT (10 + 2 * CellLength + CellLength / 2, YMax - CellHeight * 22), Pill(Clock(1)), PSET

END SUB

SUB GetPill (PillColours(), Clock(), Colour1, Colour2)
'*******************************************************************
' Function: Returns the pill colour and positions stored in Clock()
'*******************************************************************

RANDOMIZE TIMER

Pill = INT(RND * 6) + 1

SELECT CASE Pill
  CASE 1   ' Simply Blue
    CurrPill = 1                  '' Since all pills are stored in one
    Clock(1) = CurrPill           '' array Pill(), we have to find each
    Clock(2) = CurrPill + 80      '' corresponding pill and match it with
    Clock(3) = CurrPill           '' the corresponding colour.
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(1, 1)
    Colour2 = PillColours(1, 1)
  CASE 2   ' Simply Red
    CurrPill = 8 * 80 + 1         '' Position in Pill() where Red is found
    Clock(1) = CurrPill
    Clock(2) = CurrPill + 80
    Clock(3) = CurrPill
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(2, 1)
    Colour2 = PillColours(2, 1)
  CASE 3   ' Simply Yellow
    CurrPill = 16 * 80 + 1
    Clock(1) = CurrPill
    Clock(2) = CurrPill + 80
    Clock(3) = CurrPill
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(3, 1)
    Colour2 = PillColours(3, 1)
  CASE 4  ' Blue and Red
    CurrPill = 2 * 80 + 1
    Clock(1) = CurrPill
    Clock(2) = CurrPill + 80
    CurrPill = 6 * 80 + 1
    Clock(3) = CurrPill
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(1, 1)
    Colour2 = PillColours(2, 1)
  CASE 5  ' Blue and Yellow
    CurrPill = 4 * 80 + 1
    Clock(1) = CurrPill
    Clock(2) = CurrPill + 80
    CurrPill = 12 * 80 + 1
    Clock(3) = CurrPill
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(1, 1)
    Colour2 = PillColours(3, 1)
  CASE 6  ' Red and Yellow
    CurrPill = 10 * 80 + 1
    Clock(1) = CurrPill
    Clock(2) = CurrPill + 80
    CurrPill = 14 * 80 + 1
    Clock(3) = CurrPill
    Clock(4) = CurrPill + 80
    Colour1 = PillColours(2, 1)
    Colour2 = PillColours(3, 1)
END SELECT

END SUB

SUB InitGrid (Grid(), GridJoin())

FOR I = 1 TO Height
  FOR J = 1 TO Cells * 2
    Grid(J, I) = 0
    GridJoin(J, I) = 0
  NEXT J
NEXT I

END SUB

SUB InitJoystick (JoyStick() AS JoyType)
'*************************************************
' Function: Simple calibration of the joystick.
'           No detection routines implemented.
'*************************************************

PRINT "Joystick Calibration, please wait..."

FOR I = 1 TO 10       '' Get normal values (idle state)
  A = STICK(0)
  B = STICK(1)
  IF JoyStick(1).InitialX < A THEN JoyStick(1).InitialX = A
  IF JoyStick(1).InitialY < B THEN JoyStick(1).InitialY = B
NEXT I

JoyStick(2).InitialX = JoyStick(1).InitialX - 1  '' To make things more consistent
JoyStick(2).InitialY = JoyStick(1).InitialY - 1  '' get two values
JoyStick(1).Up = JoyStick(1).InitialY
JoyStick(1).Left = JoyStick(1).InitialX

WHILE STRIG(0) OR STRIG(1): WEND

PRINT "Move joystick to upper left corner then press botton"

DO
  A = STICK(0)   '' XCoord
  B = STICK(1)   '' YCoord
  IF JoyStick(1).Up > B THEN JoyStick(1).Up = B
  IF JoyStick(1).Left > A THEN JoyStick(1).Left = A
LOOP UNTIL STRIG(0) OR STRIG(1)

JoyStick(2).Up = JoyStick(1).Up + 1
JoyStick(2).Left = JoyStick(1).Left - 1

PRINT "Move joystick to lower right corner then press botton"

WHILE STRIG(0) OR STRIG(1)  '' Depressed status
WEND

DO
  A = STICK(0)
  B = STICK(1)
  IF JoyStick(1).Down < B THEN JoyStick(1).Down = B
  IF JoyStick(1).Right < A THEN JoyStick(1).Right = A
LOOP UNTIL STRIG(0) OR STRIG(1)

JoyStick(2).Down = JoyStick(1).Down - 1
JoyStick(2).Right = JoyStick(1).Right - 1

END SUB

SUB InitPills (Pill(), PillColours())
'*****************************************************************
' Function: Draws the pills on the screen, then save them in the
'           array Pill()
'*****************************************************************

X = 4: Y = 4: N = 1
FOR I = 1 TO 3
  FOR J = 1 TO 3
    LINE (0, 0)-(CellLength, CellHeight), 0, BF
    DrawPillHorz X, Y, PillColours(I, 1), PillColours(J, 1), PillColours(I, 2), PillColours(J, 2)
    GET (1, 0)-(CellLength, CellHeight - 1), Pill(N)
    LINE (0, 0)-(CellLength / 2, CellHeight * 2), 0, BF
    DrawPillVert X, Y, PillColours(I, 1), PillColours(J, 1), PillColours(I, 2), PillColours(J, 2)
    N = N + 80
    GET (1, 0)-(CellLength / 2, CellHeight * 2 - 1), Pill(N)
    N = N + 80
  NEXT J
NEXT I

END SUB

FUNCTION MatchHorizontal% (Grid(), SX, SY)
'*************************************************************************
' Function: Scans through the entire grid, looking for a horizontal match.
'*************************************************************************

Y = 0
WHILE (Y < Height) AND (Count < MatchIt)   '' We want to check for any
  X = 0                                    '' horizontal matches first,
  Y = Y + 1                                '' starting from the bottom up
  Count = 0
  Last = 0
  WHILE (X < Cells * 2) AND (Count < MatchIt)  '' Check from left to right
    X = X + 1
    IF Grid(X, Y) <> 0 THEN                '' If Not empty, check it out
      IF Last = ABS(Grid(X, Y)) THEN       '' Germs are given negative signs
        Count = Count + 1
      ELSE                                 '' New colour
        Last = ABS(Grid(X, Y))             '' Store it
        Count = 1                          '' Begin new counter
        SX = X: SY = Y                     '' New locations
      END IF
    ELSE                                   '' Empty grid
      Last = 0                             '' No colour
      Count = 1                            '' New counter
    END IF
  WEND
WEND

IF Count = MatchIt THEN MatchHorizontal = True ELSE MatchHorizontal = False

END FUNCTION

FUNCTION MatchVertical% (Grid(), SX, SY)
'*************************************************************************
' Function: Scans through the entire grid, looking for a vertical match.
'*************************************************************************

X = 0
WHILE (X < Cells * 2) AND (Count < MatchIt) '' We want to check for any
  Y = Height + 1                            '' vertical matches
  X = X + 1                                 '' starting from the left to right
  Count = 0
  Last = 0
  WHILE (Y > 1) AND (Count < MatchIt)       '' Check from bottom up
    Y = Y - 1
    IF Grid(X, Y) <> 0 THEN                 '' If Not empty, check it out
      IF Last = ABS(Grid(X, Y)) THEN        '' Germs are given negative signs
        Count = Count + 1
        SY = Y                              '' Store current Y Position
      ELSE                                  '' New colour
        Last = ABS(Grid(X, Y))              '' Store it
        Count = 1                           '' Begin new counter
        SX = X: SY = Y                      '' New locations
      END IF
    ELSE                                    '' Empty grid
      Last = 0                              '' No colour
      Count = 1                             '' New counter
    END IF
  WEND
WEND

IF Count = MatchIt THEN MatchVertical = True ELSE MatchVertical = False

END FUNCTION

SUB RandCycle (Cycle)

RANDOMIZE TIMER
C = INT(RND * 2) + 1
IF C = 1 THEN Cycle = 1 ELSE Cycle = 3

END SUB

SUB RandGerms (Grid(), PillColours(), OrigX, OrigY, Germs, Level)

Count = 0
DO
  RANDOMIZE TIMER
  X = INT(RND * Cells * 2) + 1
  YY = INT(Level * RND)
  Y = Height - YY
  IF Grid(X, Y) = 0 THEN
    C = INT(RND * Colours) + 1
    Count = Count + 1
    StartX = OrigX + ((X - 1) * CellLength / 2)
    StartY = OrigY + (CellHeight * (Y - 1))
    CIRCLE (StartX + 6, StartY + 2), 4, PillColours(C, 1), 0, 3.3, 1 / 2
    CIRCLE (StartX + 6, StartY + CellHeight - 3), 4, PillColours(C, 1), 3, 0, 1 / 2
    CIRCLE (StartX + CellLength / 2 - 4, StartY + 4), 4, PillColours(C, 1), 4.5, 1.7, 1.5
    CIRCLE (StartX + 3, StartY + 4), 4, PillColours(C, 1), 1.7, 4.9, 1.5
    PAINT (StartX + 4, StartY + 2), PillColours(C, 1)
    PSET (StartX + 4, StartY + 2), 0
    PSET (StartX + 3, StartY + 2), 0
    PSET (StartX + 8, StartY + 2), 0
    PSET (StartX + 9, StartY + 2), 0
    CIRCLE (StartX + 6, StartY + 7), 3, 0, 0, 3, 1 / 2
    Grid(X, Y) = -PillColours(C, 1)
  END IF
LOOP UNTIL Count = Germs


END SUB

SUB SetBoundary (StartX, StartY, EndX, EndY, BoundY, BoundXMin, BoundXMax)
'*****************************************************************
' Function: To set the boundaries of the playing field
'*****************************************************************

XMid = XMax / 2
YMid = YMax / 2

StartX = XMid - (CellLength * Cells / 2)
EndX = XMid + (CellLength * Cells / 2)

StartY = YMid - (CellHeight * Height / 2)
EndY = YMid + (CellHeight * Height / 2)

BoundXMin = StartX
BoundXMax = EndX - CellLength
BoundY = EndY - 2 * CellHeight

END SUB

SUB SetColours (PillColours())

PillColours(1, 1) = 1   '' Pill colour
PillColours(1, 2) = 9   '' Reflection colour
PillColours(2, 1) = 4   '' Pill colour
PillColours(2, 2) = 12  ''  .
PillColours(3, 1) = 14  ''  .
PillColours(3, 2) = 15  ''  .

END SUB

