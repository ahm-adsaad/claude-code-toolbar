#ifndef CPROCINFO_H
#define CPROCINFO_H

#include <stdint.h>
#include <sys/types.h>

/// Pid owning an IPv4/IPv6 TCP socket whose local port is `localPort` and remote port is `remotePort`, or 0.
pid_t cproc_pid_for_tcp(uint16_t localPort, uint16_t remotePort);

/// Parent pid, or -1 when the process is unknown or not readable.
pid_t cproc_parent(pid_t pid);

/// Seconds since the epoch when the process started, or 0.
int64_t cproc_start_time(pid_t pid);

/// Copies the short process name into buf (NUL-terminated); returns its length, or -1.
int cproc_name(pid_t pid, char *buf, int len);

#endif
