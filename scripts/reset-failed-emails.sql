-- Re-queue email that failed while SMTP was off or unreachable.
-- Run only when smtp4dev (or the real transport) is up and no dispatch is in flight.

UPDATE Notifications
SET Status = 'Pending', Attempts = 0, LastError = NULL
WHERE Channel = 'Email' AND Status = 'Failed';
