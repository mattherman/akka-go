module UserInterface

open System

let join (separator: string) (stringList: string seq) =
    String.Join(separator, stringList)

let logo = 
    [
        """ ______     __  __     __  __     ______        ______     ______    """
        """/\  __ \   /\ \/ /    /\ \/ /    /\  __ \      /\  ___\   /\  __ \   """
        """\ \  __ \  \ \  _"-.  \ \  _"-.  \ \  __ \     \ \ \__ \  \ \ \/\ \  """
        """ \ \_\ \_\  \ \_\ \_\  \ \_\ \_\  \ \_\ \_\     \ \_____\  \ \_____\ """
        """  \/_/\/_/   \/_/\/_/   \/_/\/_/   \/_/\/_/      \/_____/   \/_____/ """
        """                                                                     """
    ] |> join Environment.NewLine

let authenticationPrompt =
    "\nPlease enter your username: "

let helpMenu =
    [
        "Commands:"
        "\tstart - Starts a new game of Go"
        "\tjoin <id> - Joins a game with the specified id"
        "\tlist - Lists available games"
        "\thelp - List available commands\n"
    ] |> join Environment.NewLine 