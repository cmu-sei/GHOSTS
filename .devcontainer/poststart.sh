#!/bin/bash

# Purple → neon green fade (GHOSTS)
P1='\033[38;5;93m'    # deep purple
P2='\033[38;5;129m'   # hot purple
P3='\033[38;5;84m'    # mint green
P4='\033[38;5;46m'    # neon green
RST='\033[0m'

printf "%b" "
${P4}
                   ('-. .-.               .-')    .-') _     .-')
                 ( OO )  /              ( OO ). (  OO) )   ( OO ).
        ,----.    ,--. ,--. .-'),-----. (_)---\\_)/     '._ (_)---\\_)
       '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |
       |  |_( O- )|   .|  |/   |  | |  |\\  :\` \`. '--.  .--'\\  :\` \`.
${P1}       |  | .--, \\|       |\\_) |  |\\|  | '..\`''.)   |  |    '..\`''.)
      (|  | '. (_/|  .-.  |  \\ |  | |  |.-._)   \\   |  |   .-._)   \\
       |  '--'  | |  | |  |   \`'  '-'  '\\       /   |  |   \\       /
       \`------'  \`--' \`--'     \`-----'  \`-----'    \`--'    \`-----'
${RST}

${P4}
              Welcome to the GHOSTS Development Container!
Type Ctrl-Shift-\` (backtick) to open a new terminal and get started building. 🤓👻
\033[0m
"
