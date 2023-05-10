BOF := Askcreds
CC_x64 := x86_64-w64-mingw32-gcc
#STRIP_x64 := x86_64-w64-mingw32-strip

all:
	$(CC_x64) -o ask_creds.x64.o -c $(BOF).c
	$(STRIP_x64) --strip-unneeded ask_creds.x64.o

clean:
	rm ../$(BOF).o
