# chiptool
low-level modification tool

[![Total Downloads](https://img.shields.io/github/downloads/LuSlower/chiptool/total.svg)](https://github.com/LuSlower/chiptool/releases)

```
Usage:

MSR Commands
  --rdmsr    <address>                                           | Read MSR
  --wrmsr    <address> <edx> <eax>                               | Write MSR
  --rdmsrb   <address> <bit>                                     | Read MSR Bit
  --wrmsrb   <address> <bit> <value>                             | Write MSR Bit

PCI Commands
  --rdpci    <size> <bus:dev:func> <offset>                      | Read PCI configuration
  --wrpci    <size> <bus:dev:func> <offset> <value>              | Write PCI configuration
  --rdpcib   <size> <bus:dev:func> <offset> <bit>                | Read PCI Bit
  --wrpcib   <size> <bus:dev:func> <offset> <bit> <value>        | Write PCI Bit

I/O Commands
  --rdio     <size> <port>                                       | Read I/O port
  --wrio     <size> <port> <value>                               | Write I/O port
  --rdiob    <size> <port> <bit>                                 | Read I/O Port Bit
  --wriob    <size> <port> <bit> <value>                         | Write I/O Port Bit

Memory Commands
  --rdmem    <size> <address>                                    | Read Memory
  --wrmem    <size> <address> <value>                            | Write Memory
  --rdmemb   <size> <address> <bit>                              | Read Memory Bit
  --wrmemb   <size> <address> <bit> <value>                      | Write Memory Bit

PMC Commands
  --rdpmc    <index>                                             | Read PMC Counter

Help
  --help or /?                                                   | Show this help menu
```
