package com.example.sdmagent

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.util.Log
import android.widget.Toast

class InstallReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE)
        val msg = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE)
        val packageName = intent.getStringExtra(PackageInstaller.EXTRA_PACKAGE_NAME)

        when (status) {
            PackageInstaller.STATUS_SUCCESS -> {
                Log.d("InstallReceiver", "Install succeeded for $packageName")
                Toast.makeText(context, "Install succeeded: $packageName", Toast.LENGTH_SHORT).show()
            }
            PackageInstaller.STATUS_PENDING_USER_ACTION -> {
                val confirmIntent = intent.getParcelableExtra<Intent>(Intent.EXTRA_INTENT)
                confirmIntent?.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                context.startActivity(confirmIntent)
            }
            else -> {
                Log.e("InstallReceiver", "Install failed for $packageName: $msg ($status)")
                Toast.makeText(context, "Install failed: $msg", Toast.LENGTH_LONG).show()
            }
        }
    }
}
