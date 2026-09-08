#include "cprocinfo.h"
#include <libproc.h>
#include <sys/proc_info.h>
#include <arpa/inet.h>
#include <stdlib.h>
#include <string.h>

static int bsd_info(pid_t pid, struct proc_bsdinfo *info) {
    return proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, info, sizeof(*info)) == (int)sizeof(*info);
}

pid_t cproc_parent(pid_t pid) {
    struct proc_bsdinfo info;
    return bsd_info(pid, &info) ? (pid_t)info.pbi_ppid : -1;
}

int64_t cproc_start_time(pid_t pid) {
    struct proc_bsdinfo info;
    return bsd_info(pid, &info) ? (int64_t)info.pbi_start_tvsec : 0;
}

int cproc_name(pid_t pid, char *buf, int len) {
    struct proc_bsdinfo info;
    if (!bsd_info(pid, &info) || len <= 0) return -1;
    strncpy(buf, info.pbi_comm, (size_t)len - 1);
    buf[len - 1] = '\0';
    return (int)strlen(buf);
}

static int socket_matches(pid_t pid, int fd, uint16_t localPort, uint16_t remotePort) {
    struct socket_fdinfo si;
    if (proc_pidfdinfo(pid, fd, PROC_PIDFDSOCKETINFO, &si, sizeof(si)) != (int)sizeof(si)) return 0;
    if (si.psi.soi_kind != SOCKINFO_TCP) return 0;
    struct in_sockinfo *in = &si.psi.soi_proto.pri_tcp.tcpsi_ini;
    return ntohs((uint16_t)in->insi_lport) == localPort && ntohs((uint16_t)in->insi_fport) == remotePort;
}

static int pid_has_socket(pid_t pid, uint16_t localPort, uint16_t remotePort) {
    int size = proc_pidinfo(pid, PROC_PIDLISTFDS, 0, NULL, 0);
    if (size <= 0) return 0;
    struct proc_fdinfo *fds = malloc((size_t)size);
    if (!fds) return 0;
    int got = proc_pidinfo(pid, PROC_PIDLISTFDS, 0, fds, size);
    int count = got > 0 ? got / (int)sizeof(struct proc_fdinfo) : 0;
    int found = 0;
    for (int i = 0; i < count && !found; i++) {
        if (fds[i].proc_fdtype == PROX_FDTYPE_SOCKET && socket_matches(pid, fds[i].proc_fd, localPort, remotePort)) found = 1;
    }
    free(fds);
    return found;
}

pid_t cproc_pid_for_tcp(uint16_t localPort, uint16_t remotePort) {
    int count = proc_listallpids(NULL, 0);
    if (count <= 0) return 0;
    int capacity = count + 64;
    pid_t *pids = calloc((size_t)capacity, sizeof(pid_t));
    if (!pids) return 0;
    count = proc_listallpids(pids, capacity * (int)sizeof(pid_t));
    pid_t result = 0;
    /* Claude Code is a node process; checking the likely names first makes a hit cost a handful of lookups. */
    for (int pass = 0; pass < 2 && result == 0; pass++) {
        for (int i = 0; i < count && result == 0; i++) {
            if (pids[i] <= 0) continue;
            if (pass == 0) {
                char name[64];
                if (cproc_name(pids[i], name, (int)sizeof(name)) <= 0) continue;
                if (strcmp(name, "claude") != 0 && strcmp(name, "node") != 0) continue;
            }
            if (pid_has_socket(pids[i], localPort, remotePort)) result = pids[i];
        }
    }
    free(pids);
    return result;
}
