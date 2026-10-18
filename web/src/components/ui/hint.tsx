import * as React from "react"
import { Popover as PopoverPrimitive, Tooltip as TooltipPrimitive } from "radix-ui"

import { cn } from "@/lib/utils"
import { useHasHover } from "@/hooks/useHasHover"

/**
 * Hover-or-tap hint: renders a Radix Tooltip on fine-pointer/hover devices and a
 * tap-opened Popover (styled like the tooltip bubble) on touch devices.
 *
 * Radix Tooltip intentionally never opens on touch (pointerType "touch" is ignored,
 * click only closes), so a Tooltip-only trigger with no action of its own is dead
 * UI on mobile. Use Hint for info-only triggers; for triggers that perform an
 * action keep Tooltip for desktop and surface the info another way (inline text).
 * See radix-ui/primitives#955 and shadcn-ui/ui#86.
 */

type HintMode = { hasHover: boolean }
const HintModeContext = React.createContext<HintMode>({ hasHover: true })

interface HintProps {
  /** Open delay on hover devices, ms; tap-to-open Popover ignores it */
  delayDuration?: number
  children: React.ReactNode
}

function Hint({ delayDuration = 100, children }: HintProps) {
  const hasHover = useHasHover()
  return (
    <HintModeContext.Provider value={{ hasHover }}>
      {hasHover ? (
        <TooltipPrimitive.Provider>
          <TooltipPrimitive.Root delayDuration={delayDuration}>{children}</TooltipPrimitive.Root>
        </TooltipPrimitive.Provider>
      ) : (
        <PopoverPrimitive.Root>{children}</PopoverPrimitive.Root>
      )}
    </HintModeContext.Provider>
  )
}

type HintTriggerProps = Omit<React.ComponentProps<typeof PopoverPrimitive.Trigger>, "asChild"> & {
  asChild?: boolean
}

function HintTrigger({ asChild = true, ...props }: HintTriggerProps) {
  const { hasHover } = React.useContext(HintModeContext)
  return hasHover ? (
    <TooltipPrimitive.Trigger asChild={asChild} {...props} />
  ) : (
    <PopoverPrimitive.Trigger asChild={asChild} {...props} />
  )
}

interface HintContentProps {
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  align?: "start" | "center" | "end"
  className?: string
  children: React.ReactNode
}

const SURFACE =
  "z-50 w-fit animate-in rounded-md bg-foreground px-3 py-1.5 text-xs text-balance text-background fade-in-0 zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95"

const ARROW = "z-50 size-2.5 translate-y-[calc(-50%_-_2px)] rotate-45 rounded-[2px] bg-foreground fill-foreground"

function HintContent({ side = "top", sideOffset = 0, align = "center", className, children }: HintContentProps) {
  const { hasHover } = React.useContext(HintModeContext)
  const ariaLabel = typeof children === "string" ? children : undefined

  if (hasHover) {
    return (
      <TooltipPrimitive.Portal>
        <TooltipPrimitive.Content
          data-slot="hint-content"
          side={side}
          sideOffset={sideOffset}
          align={align}
          className={cn(SURFACE, "origin-(--radix-tooltip-content-transform-origin)", className)}
        >
          {children}
          <TooltipPrimitive.Arrow className={ARROW} />
        </TooltipPrimitive.Content>
      </TooltipPrimitive.Portal>
    )
  }

  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        data-slot="hint-content"
        side={side}
        sideOffset={sideOffset}
        align={align}
        aria-label={ariaLabel}
        className={cn(SURFACE, "origin-(--radix-popover-content-transform-origin) outline-hidden", className)}
      >
        {children}
        <PopoverPrimitive.Arrow className={ARROW} />
      </PopoverPrimitive.Content>
    </PopoverPrimitive.Portal>
  )
}

export { Hint, HintTrigger, HintContent }
