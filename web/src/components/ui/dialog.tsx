import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { cva, type VariantProps } from 'class-variance-authority';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

const Dialog = DialogPrimitive.Root;
const DialogTrigger = DialogPrimitive.Trigger;
const DialogPortal = DialogPrimitive.Portal;
const DialogClose = DialogPrimitive.Close;

const DialogOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-50 bg-black/80',
      'data-[state=open]:animate-in data-[state=closed]:animate-out',
      'data-[state=open]:fade-in-0 data-[state=closed]:fade-out-0',
      className
    )}
    {...props}
  />
));
DialogOverlay.displayName = DialogPrimitive.Overlay.displayName;

const dialogContentVariants = cva(
    [
      'fixed z-50',
      'border bg-background shadow-lg rounded-lg',
      'flex flex-col overflow-y-auto',
      // opacity-only animation: the custom translate-based positioning above
      // must not be overridden by transform-based enter/exit keyframes
      'data-[state=open]:animate-in data-[state=closed]:animate-out',
      'data-[state=open]:fade-in-0 data-[state=closed]:fade-out-0',
    ],
  {
    variants: {
      size: {
        default: [
          // Mobile: full-screen card with explicit inset margins on all sides,
          // kept clear of the safe areas (status bar / home indicator)
          'left-3 right-3 top-[calc(0.75rem+var(--safe-t))] bottom-[calc(0.75rem+var(--safe-b))]',
          // Desktop: horizontally centered card (left-1/2 + translate, same as alert-dialog)
          'sm:left-1/2 sm:-translate-x-1/2 sm:right-auto sm:top-[8%] sm:bottom-auto',
          'sm:w-[80%] sm:max-w-lg',
        ],
        // Chart: desktop gets a wide card; mobile fits the content height (no
        // bottom inset) so there is no empty space under a landscape image, and
        // the card is vertically centered (top-1/2 + translate) instead of pinned
        // to the top of the screen.
        chart: [
          'left-3 right-3 top-1/2 -translate-y-1/2 max-h-[var(--dialog-max-h)]',
          'sm:left-1/2 sm:-translate-x-1/2 sm:right-auto sm:top-1/2 sm:bottom-auto',
          'sm:w-[80%] sm:max-w-4xl',
        ],
        // Fullscreen: fills the whole viewport, kept clear of the top/bottom safe areas
        // (notch / home indicator) so the chart and the close button stay reachable.
        // No translate-based positioning, so the opacity-only animation applies as-is.
        fullscreen: [
          'left-0 right-0 top-[var(--safe-t)] bottom-[var(--safe-b)] rounded-none sm:left-0 sm:right-0 sm:top-[var(--safe-t)] sm:bottom-[var(--safe-b)] sm:translate-x-0',
        ],
      },
    },
    defaultVariants: { size: 'default' },
  }
);

// The close button lives in the same coordinate box as the card, so its alignment is
// per-size: the fullscreen chart dialogs carry a 44px toolbar line ("AAPL DAY ..." title
// + settings gear, min-h-11) as their top chrome, and the X must sit on that line's
// center (and itself be a 44px touch target, project rule): the row center is 38px from
// the card top on mobile (p-4 + 22) and 46px on desktop (sm:p-6 + 22) - a size-11 X at
// top-4 / sm:top-6 centers there. The hover look matches the toolbar's ghost gear
// (Button ghost: rounded-md + accent background), and the toolbars' pr-11 sm:pr-9
// spacing puts the X exactly one gap-2 from it on both paddings
const dialogCloseVariants = cva(
    [
      'absolute z-10 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100',
      'focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:pointer-events-none',
    ],
  {
    variants: {
      size: {
        default: 'right-4 top-4',
        chart: 'right-4 top-4',
        fullscreen: [
          'right-2 top-4 flex size-11 items-center justify-center',
          'rounded-md transition-[color,background-color,opacity] hover:bg-accent hover:text-accent-foreground',
          'sm:top-6',
        ],
      },
    },
    defaultVariants: { size: 'default' },
  }
);

export interface DialogContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content>,
    VariantProps<typeof dialogContentVariants> {}

const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  DialogContentProps
>(({ className, children, size, ...props }, ref) => (
  <DialogPortal>
    <DialogOverlay />
    <DialogPrimitive.Content
      ref={ref}
      className={cn(
        dialogContentVariants({ size }),
        className
      )}
      {...props}
    >
      <div className="flex-1 p-4 sm:p-6">
        {children}
      </div>
      {/* cn merges the fullscreen overrides (rounded-md, transition) over the base classes */}
      <DialogPrimitive.Close className={cn(dialogCloseVariants({ size }))}>
        <X className={size === 'fullscreen' ? 'size-6' : 'h-4 w-4'} />
        <span className="sr-only">Close</span>
      </DialogPrimitive.Close>
    </DialogPrimitive.Content>
  </DialogPortal>
));
DialogContent.displayName = DialogPrimitive.Content.displayName;

const DialogHeader = ({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
  <div className={cn('flex flex-col space-y-1.5 text-center sm:text-left', className)} {...props} />
);
DialogHeader.displayName = 'DialogHeader';

const DialogFooter = ({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
  <div className={cn('mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end sm:gap-0 sm:space-x-2', className)} {...props} />
);
DialogFooter.displayName = 'DialogFooter';

const DialogTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title ref={ref} className={cn('text-lg font-semibold leading-none tracking-tight', className)} {...props} />
));
DialogTitle.displayName = DialogPrimitive.Title.displayName;

const DialogDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description ref={ref} className={cn('text-sm text-muted-foreground', className)} {...props} />
));
DialogDescription.displayName = DialogPrimitive.Description.displayName;

export { Dialog, DialogPortal, DialogOverlay, DialogClose, DialogTrigger, DialogContent, DialogHeader, DialogFooter, DialogTitle, DialogDescription };
