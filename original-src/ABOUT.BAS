DECLARE SUB credits (length%, clr%, over%, delay!, start%)

DIM SHARED scrolltxt(1 TO 50) AS STRING

CLS : ' SCREEN
RANDOMIZE TIMER

'            This number is how many lines you want
'            ≥
'            
FOR i = 1 TO 24
 READ scrolltxt$(i)
NEXT i                   ' .3 is about a 1/3 a second
                         '
                         '
CALL credits(100, 4, -1, .3, 0)

DATA "****************************************"
DATA "***********************************"
DATA "******************"
DATA "***********************************"
DATA "****************************************"
DATA "****************************************"
DATA "****************************************"
DATA "******                            ******"
DATA "******                            ******"
DATA "******       OSB Vers∆o 0.2       ******"
DATA "******   Operating System Basic   ******"
DATA "****** Sistema Operacional Basico ******"
DATA "******                            ******"
DATA "******                            ******"
DATA "******         CRêDITOS           ******"
DATA "******                            ******"
DATA "******                            ******"
DATA "******      www.osb.rg3.net       ******"
DATA "******                            ******"
DATA "******                            ******"
DATA "******                            ******"
DATA "****************************************"
DATA "****************************************"
DATA "******************************"
22
SELECT CASE ERR
        CASE ELSE
                END
        END SELECT

SUB credits (length%, clr%, over%, delay!, start%)
ON ERROR GOTO 22

 down% = 24 - start%: begin% = 1

 FOR i% = 1 TO length% - 25
  down% = down% - 1
  IF down% < 1 THEN down% = 1
  upness% = 0
 
  FOR a% = 0 TO (24 - down%)
   IF over% = -1 THEN
    ovr% = LEN(scrolltxt$(begin% + a%))
    ovr% = (40 - ovr%) / 2
   ELSE
    ovr% = over%
   END IF
   IF ovr% = 0 THEN ovr% = 1

   COLOR clr%
   LOCATE down% + upness%, ovr%
   PRINT scrolltxt$(begin% + a%)
  
   upness% = upness% + 1

  NEXT a%
 
  IF down% > 1 THEN
   begin% = 1
  ELSE
   begin% = begin% + 1
  END IF
 
  CurrentTimer! = TIMER
  WHILE TIMER < (CurrentTimer! + delay!)
  WEND

 NEXT i%


END SUB

