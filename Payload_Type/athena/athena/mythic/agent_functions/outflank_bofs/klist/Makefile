BOF := Klist
CC_x64 := x86_64-w64-mingw32-gcc
STRIP_x64 := x86_64-w64-mingw32-strip

all:
	$(CC_x64) -o klist.x64.o -c $(BOF).c
	$(STRIP_x64) --strip-unneeded klist.x64.o
clean:
	rm ../$(BOF).o
