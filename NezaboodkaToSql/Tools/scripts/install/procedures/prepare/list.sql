CREATE TABLE `${db_name}`.`list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`field_id` INT NOT NULL,

	FOREIGN KEY (`owner_id`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE,	-- cleanup list of deleted object

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE	-- delete list when it's field info is removed
) ENGINE=`INNODB`;
