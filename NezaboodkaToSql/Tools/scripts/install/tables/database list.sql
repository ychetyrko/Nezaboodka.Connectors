/*	Databases list

	User rights:
		0 - ReadWrite
		1 - ReadOnly
		2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE
		CHECK(`name` != ''),
	`access` TINYINT UNSIGNED NOT NULL DEFAULT 0	-- ReadWrite
		CHECK(`access` < 3),
	`is_removed` BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=`INNODB` COLLATE `UTF8_GENERAL_CI`;
