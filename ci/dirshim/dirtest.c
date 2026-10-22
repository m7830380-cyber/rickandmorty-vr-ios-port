// Lists a folder the way Unity 5.4 does (getdirentriesattr) and prints what comes back.
//   dirtest <folder>        (run with DYLD_INSERT_LIBRARIES=dirshim.dylib to test the shim)
#include <sys/attr.h>
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>

int main(int argc, char **argv) {
    if (argc < 2) return 2;
    int fd = open(argv[1], O_RDONLY);
    if (fd < 0) { perror("open"); return 1; }
    struct attrlist al = {0};
    al.bitmapcount = ATTR_BIT_MAP_COUNT;
    al.commonattr = ATTR_CMN_NAME | ATTR_CMN_OBJTYPE | ATTR_CMN_MODTIME;
    al.fileattr = ATTR_FILE_DATALENGTH;
    char buf[8192];
    int total = 0;
    for (;;) {
        unsigned int count = 4, base = 0, state = 0;  // small batches to exercise the paging
        int r = getdirentriesattr(fd, &al, buf, sizeof buf, &count, &base, &state, 0);
        if (r < 0) { printf("getdirentriesattr failed: %s\n", strerror(errno)); return 1; }
        char *p = buf;
        for (unsigned int i = 0; i < count; i++) {
            uint32_t len = *(uint32_t *)p;
            attrreference_t *name = (attrreference_t *)(p + 4);
            fsobj_type_t type = *(fsobj_type_t *)(p + 4 + sizeof(attrreference_t));
            printf("  %-30s type=%u len=%u\n", (char *)name + name->attr_dataoffset, type, len);
            p += len;
            total++;
        }
        if (r == 1 || count == 0) break;
    }
    printf("%d entries in %s\n", total, argv[1]);
    close(fd);
    return 0;
}
