module DiagramStyle

open Fable.Helpers.React.Props

let headerHeight = "52px"
let rightSectionWidth = "400px"

let navbarStyle = Style [
    Width "100%"
    Height headerHeight
]

let rightSectionStyle = Style [
    Position "fixed"
    Right "0px"
    Height (sprintf "calc(100%s - %s)" "%" headerHeight) // WindowSize - headerHeight
    Width rightSectionWidth
    OverflowX "hidden"
    OverflowY "scroll"
    BorderTop "2px solid lightgray"
]

let canvasHiddenStyle = Style [
    Display "none"
]

let canvasVisibleStyle = Style [
    Display "block"
    Position "absolute" // Required to work.
    Overflow "scroll"
    Top headerHeight // Placed just under the header.
    Left "0px"
    Bottom "0px"
    Right rightSectionWidth
    BorderTop "2px solid lightgray"
]

let canvasSmallMenuStyle = Style [
    Display "block"
    Position "absolute" // Required to work.
    Overflow "hidden"
    Left "10px"
    Bottom "25px"
    Right (sprintf "calc(100%s - 300px)" "%")
]

let canvasSmallButtonStyle = Style [
    MarginRight "5px"
    BackgroundColor "white"
    BorderRadius "4px"
    BorderStyle "solid"
    Outline "none"
    Padding "4px"
    Opacity 0.7
]

let private notificationStyle = [
    ZIndex 100 // In front of everything.
    MinWidth "300px"
    Position "absolute"
    Right "20px"
    Bottom "20px"
    Padding "20px"
]

let errorNotificationStyle =
    Style <| notificationStyle @ [
        Color "white" // White text.
        BackgroundColor "red"
    ]

let simulationNumberStyle = Style [
    Width "100px"
    Height "30px"
]

let simulationBitStyle = Style [
    Width "100px"
    Height "30px"
    PaddingTop "3px"
]

let menuLabelStyle = Style [
    Outline "none"
    MarginTop "10px"
    MarginBottom "10px"
    Color "#7a7a7a"
    FontSize "0.80em"
    LetterSpacing "0.1em"
    TextTransform "uppercase"
]
