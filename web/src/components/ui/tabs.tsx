import * as React from 'react';
import * as TabsPrimitive from '@radix-ui/react-tabs';
import { cn } from '@/lib/utils';

const Tabs = TabsPrimitive.Root;

const TabsList = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.List>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.List>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.List
    ref={ref}
    className={cn(
      // on touch the four tabs share the full width; on pointer devices the bar
      // is a compact auto-width row
      'flex w-full items-stretch gap-1 rounded-lg bg-muted p-1 text-muted-foreground md:w-auto',
      className
    )}
    {...props}
  />
));
TabsList.displayName = 'TabsList';

const TabsTrigger = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Trigger>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.Trigger>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.Trigger
    ref={ref}
    className={cn(
      // stacked icon-over-label on mobile keeps the label readable when four
      // tabs squeeze into the viewport width; a row once there is room
      'flex min-w-0 flex-1 flex-col items-center justify-center gap-1 rounded-md px-2 pt-2 pb-1.5 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm md:flex-none md:flex-row md:gap-2 md:px-3 md:text-sm',
      className
    )}
    {...props}
  />
));
TabsTrigger.displayName = 'TabsTrigger';

const TabsContent = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.Content>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.Content
    ref={ref}
    className={cn(
      // forceMount keeps every panel mounted (a panel holds unsaved drafts that a
      // tab switch must not drop), so the inactive ones hide by state, not by
      // unmounting; Radix's own hidden attribute never applies under forceMount
      'data-[state=inactive]:hidden focus-visible:outline-none',
      className
    )}
    {...props}
  />
));
TabsContent.displayName = 'TabsContent';

export { Tabs, TabsList, TabsTrigger, TabsContent };
