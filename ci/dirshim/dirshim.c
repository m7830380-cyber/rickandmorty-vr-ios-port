// Unity < 5.6 lists folders with getdirentriesattr(), which current macOS no longer serves properly
// (APFS: "error getting directory contents", HFS+: empty). This library is loaded into the editor with
// DYLD_INSERT_LIBRARIES and answers those calls with getattrlistbulk() instead.
//
// getattrlistbulk entries carry an extra attribute_set_t right after the length field (it is mandatory
// there), which getdirentriesattr entries do not have; it is cut out. attrreference offsets are relative
// to the reference itself, so they stay valid. A whole directory is read on the first call for an fd and
// handed out in pieces that respect the caller's count and buffer size.
#include <sys/attr.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#define MAXFD 1024

typedef struct {
    int used;
    dev_t dev;
    ino_t ino;
    char *buf;      // converted entries, back to back
    size_t len;
    size_t pos;     // next entry to hand out
} dirstate;

static dirstate states[MAXFD];

static void drop(dirstate *s) { free(s->buf); memset(s, 0, sizeof *s); }

static int load(int fd, struct attrlist *req, dirstate *s) {
    struct attrlist al = *req;
    al.commonattr |= ATTR_CMN_RETURNED_ATTRS;
    size_t cap = 256 * 1024;
    char *tmp = malloc(cap);
    if (!tmp) return -1;
    for (;;) {
        int n = getattrlistbulk(fd, &al, tmp, cap, FSOPT_PACK_INVAL_ATTRS);
        if (n < 0) { free(tmp); return -1; }
        if (n == 0) break;
        char *p = tmp;
        for (int i = 0; i < n; i++) {
            uint32_t elen = *(uint32_t *)p;
            uint32_t olen = elen - (uint32_t)sizeof(attribute_set_t);
            char *nb = realloc(s->buf, s->len + olen);
            if (!nb) { free(tmp); return -1; }
            s->buf = nb;
            memcpy(s->buf + s->len, &olen, 4);
            memcpy(s->buf + s->len + 4, p + 4 + sizeof(attribute_set_t), olen - 4);
            s->len += olen;
            p += elen;
        }
    }
    free(tmp);
    return 0;
}

static int shim_getdirentriesattr(int fd, void *attrList, void *attrBuf, size_t attrBufSize,
                                  unsigned int *count, unsigned int *basep, unsigned int *newState,
                                  unsigned int options) {
    (void)options;
    if (fd < 0 || fd >= MAXFD || !attrList || !attrBuf || !count) { errno = EINVAL; return -1; }
    struct stat st;
    if (fstat(fd, &st) != 0) return -1;
    dirstate *s = &states[fd];
    if (s->used && (s->dev != st.st_dev || s->ino != st.st_ino)) drop(s);
    if (!s->used) {
        s->used = 1; s->dev = st.st_dev; s->ino = st.st_ino;
        if (load(fd, (struct attrlist *)attrList, s) != 0) { int e = errno; drop(s); errno = e; return -1; }
    }
    unsigned int want = *count, got = 0;
    size_t out = 0;
    while (got < want && s->pos < s->len) {
        uint32_t elen = *(uint32_t *)(s->buf + s->pos);
        if (out + elen > attrBufSize) break;
        memcpy((char *)attrBuf + out, s->buf + s->pos, elen);
        out += elen; s->pos += elen; got++;
    }
    if (got == 0 && s->pos < s->len) { errno = ERANGE; return -1; }  // buffer too small for one entry
    *count = got;
    if (basep) *basep = (unsigned int)s->pos;
    if (newState) *newState = 0;
    if (s->pos >= s->len) { drop(s); return 1; }
    return 0;
}

// Forget a cached listing when its fd is closed, so a reused fd number starts fresh.
static int shim_close(int fd) {
    if (fd >= 0 && fd < MAXFD && states[fd].used) drop(&states[fd]);
    return close(fd);
}

__attribute__((used)) static struct { const void *repl; const void *orig; } interposers[]
    __attribute__((section("__DATA,__interpose"))) = {
        {(const void *)shim_getdirentriesattr, (const void *)getdirentriesattr},
        {(const void *)shim_close, (const void *)close},
};

__attribute__((constructor)) static void hello(void) {
    if (getenv("DIRSHIM_VERBOSE")) fprintf(stderr, "[dirshim] loaded\n");
}
